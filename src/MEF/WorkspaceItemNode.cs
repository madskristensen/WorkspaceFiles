using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EnvDTE;
using MAB.DotIgnore;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Threading;
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
        IDragDropTargetPattern
    {
        private readonly FileSystemWatcher _watcher;
        private bool _isDisposed;
        private readonly IgnoreList _ignoreList;
        private readonly Microsoft.Extensions.FileSystemGlobbing.Matcher _globbingMatcher;
        private BulkObservableCollection<WorkspaceItemNode> _children;

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
        ];

        public WorkspaceItemNode(object parent, FileSystemInfo info, IgnoreList ignoreList, Microsoft.Extensions.FileSystemGlobbing.Matcher globbingMatcher)
        {
            _ignoreList = ignoreList;
            _globbingMatcher = globbingMatcher;

            Info = info;
            SourceItem = parent;
            Type = parent is WorkspaceRootNode ? WorkspaceItemType.Root : info is FileInfo ? WorkspaceItemType.File : WorkspaceItemType.Folder;

            SetIsCut();

            if (info is DirectoryInfo dir)
            {
                HasItems = dir.EnumerateFileSystemInfos().Any();

                _watcher = new FileSystemWatcher(info.FullName);
                _watcher.Renamed += OnFileSystemChanged;
                _watcher.Deleted += OnFileSystemChanged;
                _watcher.Created += OnFileSystemChanged;
                _watcher.EnableRaisingEvents = true;
            }
        }

        public WorkspaceItemType Type { get; }
        public FileSystemInfo Info { get; set; }
        public object SourceItem { get; }
        public string Text => Info?.Name;
        public string ToolTipText => "";
        public string StateToolTipText => "";
        public object ToolTipContent => WorkspaceItemNodeTooltip.GetTooltip(this);
        public FontWeight FontWeight => FontWeights.Normal;
        public FontStyle FontStyle => FontStyles.Normal;
        public bool IsCut { get; private set; }
        public int Priority => 0;
        public bool CanPreview => Info is FileInfo;
        public ImageMoniker OverlayIconMoniker => default;
        public ImageMoniker StateIconMoniker => default;
        public bool HasItems { get; private set; }

        public IEnumerable Items
        {
            get
            {
                if (_children == null)
                {
                    RefreshChildren();
                }

                return _children;
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

        private void SetIsCut()
        {
            if (Type != WorkspaceItemType.Root && _ignoreList != null)
            {
                if (Info is DirectoryInfo dir)
                {
                    IsCut = _ignoreList.IsIgnored(dir) == true;
                }
                else if (Info is FileInfo file)
                {
                    IsCut = _ignoreList.IsIgnored(file) == true;
                }
            }
        }

        private bool ShouldShowEntry(string path)
        {
            PatternMatchingResult result = _globbingMatcher.Match(path);
            return !result.HasMatches;
        }

        private void RefreshChildren()
        {
            _children ??= [];

            if (!Info.Exists)
            {
                return;
            }

            var existingNodes = _children.ToDictionary(n => n.Info.FullName, n => n);
            var nodesToDispose = existingNodes.Keys.ToHashSet();
            List<WorkspaceItemNode> activeNodes = [];
            var entries = Directory.GetFileSystemEntries(Info.FullName);

            foreach (var entry in entries)
            {
                if (!ShouldShowEntry(entry))
                {
                    continue;
                }

                if (existingNodes.ContainsKey(entry))
                {
                    _ = nodesToDispose.Remove(entry);
                    activeNodes.Add(existingNodes[entry]);
                }
                else
                {
                    FileSystemInfo info = File.Exists(entry) ? new FileInfo(entry) : new DirectoryInfo(entry);
                    activeNodes.Add(new WorkspaceItemNode(this, info, _ignoreList, _globbingMatcher));
                }
            }

            // If there are no nodes to dispose and all active nodes are already in the children collection, we don't need to do anything
            if (nodesToDispose.Count == 0 && activeNodes.Count == _children.Count)
            {
                return;
            }

            foreach (var node in nodesToDispose)
            {
                existingNodes[node].Dispose();
            }

            // Sort the nodes so that directories are listed first, then files, and then alphabetically
            activeNodes.Sort((lhs, rhs) =>
            {
                return lhs.Info.GetType() == rhs.Info.GetType()
                    ? StringComparer.OrdinalIgnoreCase.Compare(lhs.Text, rhs.Text)
                    : lhs.Info is DirectoryInfo ? -1 : 1;
            });

            _children.BeginBulkOperation();
            _children.Clear();
            _children.AddRange(activeNodes);
            _children.EndBulkOperation();
            HasItems = _children.Any();

            //ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            //{
            RaisePropertyChanged(nameof(HasItems));
            RaisePropertyChanged(nameof(Items));
            //}).FireAndForget();
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            if (!e.FullPath.Contains(".vs") && !e.FullPath.Contains("node_modules"))
            {
                RefreshAsync().FireAndForget();
            }
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
                _watcher.Created -= OnFileSystemChanged;
                _watcher.Deleted -= OnFileSystemChanged;
                _watcher.Renamed -= OnFileSystemChanged;
                _watcher.Dispose();
            }

            if (_children != null)
            {
                foreach (WorkspaceItemNode item in _children)
                {
                    item.Dispose();
                }
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

        public async Task RefreshAsync()
        {
            if (Info is not FileInfo && Info.Exists)
            {
                await TaskScheduler.Default;
                Debouncer.Debounce(Info.FullName, RefreshChildren, 250);
            }
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
                if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
                {
                    return;
                }

                DTE dte = VS.GetRequiredService<DTE, DTE>();

                foreach (var path in paths)
                {
                    if (dte.Solution.FindProjectItem(path) != null)
                    {
                        _ = VS.MessageBox.ShowError("You cannot move a file that is part of a project");
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
    }
}