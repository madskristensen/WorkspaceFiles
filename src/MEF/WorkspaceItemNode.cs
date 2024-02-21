using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using EnvDTE;
using MAB.DotIgnore;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using WorkspaceFiles.MEF;

namespace WorkspaceFiles
{
    [DebuggerDisplay("{Text}")]
    internal class WorkspaceItemNode :
        IAttachedCollectionSource,
        ITreeDisplayItem,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IBrowsablePattern,
        IInteractionPatternProvider,
        IContextMenuPattern,
        IInvocationPattern,
        ISupportDisposalNotification,
        IDisposable,
        IRefreshPattern,
        IDragDropSourcePattern,
        IDragDropTargetPattern,
        IRenamePattern
    {
        private string _text;
        private bool _isCut;
        private FileSystemWatcher _watcher;
        private bool _isDisposed;
        private readonly IgnoreList _ignoreList;

        // Mutex to prevent multiple refreshes from happening at the same time
        Mutex _refreshChildrenMutex = new ();
        // Flag to prevent multiple refreshes from happening at the same time works in conjunction with _refreshChildrenMutex
        bool _isRefreshingChildren;

        // The visible children of this node. Any change to the file system will cause this collection to be updated.
        // This collection is observable so it can only be modified on the Main thread.
        private readonly BulkObservableCollection<WorkspaceItemNode> _children = [];

        private static readonly HashSet<Type> _supportedPatterns =
        [
            typeof(ITreeDisplayItem),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(IInvocationPattern),
            typeof(ISupportDisposalNotification),
            typeof(IRefreshPattern),
            typeof(IDragDropSourcePattern),
            typeof(IDragDropTargetPattern),
            typeof(IRenamePattern),
        ];

        public WorkspaceItemNode(object parent, FileSystemInfo info, IgnoreList ignoreList)
        {
            _ignoreList = ignoreList;

            SourceItem = parent; ;
            HasItems = info is not FileInfo;

            Info = info;
            Type = parent is WorkspaceRootNode ? WorkspaceItemType.Root : info is FileInfo ? WorkspaceItemType.File : WorkspaceItemType.Folder;
            _text = Info.Name;// Type == WorkspaceItemType.Root ? "File Explorer" : Info.Name;

            if (info is FileInfo file)
            {
                IsCut = _ignoreList?.IsIgnored(file) == true;
            }
            else if (info is DirectoryInfo dir)
            {
                IsCut = _ignoreList?.IsIgnored(dir) == true;

                _watcher = new FileSystemWatcher(Info.FullName);
                _watcher.Renamed += OnRenamed;
                _watcher.Deleted += OnDeleted;
                _watcher.Created += OnCreated;
                _watcher.EnableRaisingEvents = true;
                RefreshChildren();
            }
        }

        public WorkspaceItemType Type { get; }

        public FileSystemInfo Info { get; set; }

        public object SourceItem { get; }

        public bool HasItems { get; private set; }

        public IEnumerable Items => _children;

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    RaisePropertyChanged(nameof(Text));
                }
            }
        }

        public string ToolTipText => "";

        public string StateToolTipText => "";

        public object ToolTipContent => WorkspaceItemNodeTooltip.GetTooltip(this);

        public FontWeight FontWeight => FontWeights.Normal;

        public FontStyle FontStyle => FontStyles.Normal;

        public bool IsCut
        {
            get => _isCut;
            set
            {
                if (_isCut != value)
                {
                    _isCut = value;
                    RaisePropertyChanged(nameof(IsCut));
                }
            }
        }

        public ImageMoniker IconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return Type == WorkspaceItemType.Root ? KnownMonikers.LinkedFolderClosed : Info.GetIcon(false);
            }
        }

        public ImageMoniker ExpandedIconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return Type == WorkspaceItemType.Root ? KnownMonikers.LinkedFolderOpened : Info.GetIcon(true);
            }
        }

        public ImageMoniker OverlayIconMoniker => default;

        public ImageMoniker StateIconMoniker => default;

        public int Priority => 0;

        public bool CanPreview => Info is FileInfo;

        public IContextMenuController ContextMenuController => new WorkspaceItemContextMenuController();

        public IInvocationController InvocationController => new WorkspaceItemInvocationController();

        public IDragDropSourceController DragDropSourceController => new WorkspaceItemNodeDragDropSourceController();

        public bool IsDisposed
        {
            get => _isDisposed;
            set
            {
                if (_isDisposed != value)
                {
                    _isDisposed = value;
                    RaisePropertyChanged(nameof(IsDisposed));
                }
            }
        }

        private void ScheduleSyncChildren()
        {
            _refreshChildrenMutex.WaitOne();
            try
            {
                // If we're already refreshing the children, we don't need to do anything.
                if (_isRefreshingChildren)
                {
                    return;
                }

                _isRefreshingChildren = true;

                ThreadHelper.JoinableTaskFactory.RunAsync(SyncChildrenAsync).FireAndForget();
            }
            finally
            {
                _refreshChildrenMutex.ReleaseMutex();
            }
        }

        private async Task SyncChildrenAsync()
        {
            // Wait for a short delay to allow for multiple changes to be made before refreshing the children
            await Task.Delay(150);

            // Switch to the main thread to refresh the children
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _refreshChildrenMutex.WaitOne();
            try
            {
                RefreshChildren();
                _isRefreshingChildren = false;
            }
            finally
            {
                _refreshChildrenMutex.ReleaseMutex();
            }
        }

        private bool ShouldShowEntry(string path)
        {
            return Path.GetFileName(path) != ".git" && !path.Contains("~") && Path.GetExtension(path).ToLower() != ".tmp";
        }

        private void RefreshChildren()
        {
            Dictionary<string, WorkspaceItemNode> existingNodes = _children.ToDictionary(n => n.Info.FullName, n => n);
            HashSet<string> nodesToDispose = existingNodes.Keys.ToHashSet();
            List<WorkspaceItemNode> activeNodes = new();
            string[] entries = Directory.GetFileSystemEntries(Info.FullName);
            foreach (string entry in entries)
            {
                if (!ShouldShowEntry(entry))
                {
                    continue;
                }

                if (existingNodes.ContainsKey(entry))
                {
                    nodesToDispose.Remove(entry);
                    activeNodes.Add(existingNodes[entry]);
                }
                else
                {
                    FileSystemInfo info = File.Exists(entry) ? new FileInfo(entry) : new DirectoryInfo(entry);
                    activeNodes.Add(new WorkspaceItemNode(this, info, _ignoreList));
                }
            }

            // If there are no nodes to dispose and all active nodes are already in the children collection, we don't need to do anything
            if (nodesToDispose.Count == 0 && activeNodes.Count == _children.Count)
            {
                return;
            }

            foreach (string node in nodesToDispose)
            {
                existingNodes[node].Dispose();
            }

            // Sort the nodes so that directories are listed first, then files, and then alphabetically
            activeNodes.Sort((lhs, rhs) =>
            {
                if (lhs.Info.GetType() == rhs.Info.GetType())
                {
                    return StringComparer.OrdinalIgnoreCase.Compare(lhs.Text, rhs.Text);
                }
                return lhs.Info is DirectoryInfo ? -1 : 1;
            });

            _children.BeginBulkOperation();
            _children.Clear();
            _children.AddRange(activeNodes);
            _children.EndBulkOperation();
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            ScheduleSyncChildren();
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            ScheduleSyncChildren();
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            ScheduleSyncChildren();
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;

            if (_watcher != null)
            {
                _watcher.Created -= OnCreated;
                _watcher.Deleted -= OnDeleted;
                _watcher.Renamed -= OnRenamed;
                _watcher.Dispose();
            }

            foreach (WorkspaceItemNode item in _children)
            {
                item.Dispose();
            }
        }

        public int CompareTo(object obj)
        {
            return obj is ITreeDisplayItem item ? StringComparer.OrdinalIgnoreCase.Compare(Text, item.Text) : 0;
        }

        public object GetBrowseObject() => this;

        public TPattern GetPattern<TPattern>() where TPattern : class
        {
            if (!IsDisposed)
            {
                if (_supportedPatterns.Contains(typeof(TPattern)))
                {
                    return this as TPattern;
                }
            }
            else
            {
                // If this item has been deleted, it no longer supports any patterns
                // other than ISupportDisposalNotification.
                // It's valid to use GetPattern on a deleted item, but there are no
                // longer any pattern contracts it fulfills other than the contract
                // that reports the item as a dead ITransientObject.
                if (typeof(TPattern) == typeof(ISupportDisposalNotification))
                {
                    return this as TPattern;
                }
            }

            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Task RefreshAsync()
        {
            ScheduleSyncChildren();
            return Task.CompletedTask;
        }

        public void CancelLoad()
        {

        }

        // IDragDropSourcePattern

        public DirectionalDropArea SupportedAreas => DirectionalDropArea.On;

        public void OnDragEnter(DirectionalDropArea dropArea, DragEventArgs e)
        {
            if (Info is DirectoryInfo && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        public void OnDragOver(DirectionalDropArea dropArea, DragEventArgs e)
        {
            if (Info is DirectoryInfo && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        public void OnDragLeave(DirectionalDropArea dropArea, DragEventArgs e)
        {
            if (Info is DirectoryInfo && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        public void OnDrop(DirectionalDropArea dropArea, DragEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                DTE dte = VS.GetRequiredService<DTE, DTE>();

                foreach (var path in paths)
                {
                    if (dte.Solution.FindProjectItem(path) != null)
                    {
                        VS.MessageBox.ShowError("You cannot move a file that is part of a project");
                        return;
                    }

                    if (File.Exists(path))
                    {
                        File.Move(path, Path.Combine(Info.FullName, Path.GetFileName(path)));
                    }
                    else if (Directory.Exists(path))
                    {
                        var dir = new DirectoryInfo(path);
                        dir.MoveTo(Path.Combine(Info.FullName, dir.Name));
                    }
                }

                e.Handled = true;
            }
        }

        // IRenamePattern

        public bool CanRename => Type != WorkspaceItemType.Root;

        public IRenameItemTransaction BeginRename(object container, Func<IRenameItemTransaction, IRenameItemValidationResult> validator)
        {
            return new RenameTransaction(this, container, validator);
        }

        private class RenameTransaction : RenameItemTransaction
        {
            public RenameTransaction(WorkspaceItemNode namingRule, object container, Func<IRenameItemTransaction, IRenameItemValidationResult> validator)
                : base(namingRule, container, validator)
            {
                RenameLabel = namingRule.Text;
                Completed += (s, e) =>
                {
                    namingRule.Text = RenameLabel;
                };
            }

            public override void Commit(RenameItemCompletionFocusBehavior completionFocusBehavior)
            {
                base.Commit(completionFocusBehavior);
            }
        }
    }
}