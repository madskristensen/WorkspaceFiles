using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using EnvDTE;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

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
        private readonly BulkObservableCollection<WorkspaceItemNode> _innerItems = [];
        private string _text;
        private bool _isCut;
        private FileSystemWatcher _watcher;
        private bool _isDisposed;
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

        public WorkspaceItemNode(object parent, FileSystemInfo info)
        {
            SourceItem = parent; ;
            HasItems = info is not FileInfo;

            Info = info;
            Type = parent is WorkspaceRootNode ? WorkspaceItemType.Root : info is FileInfo ? WorkspaceItemType.File : WorkspaceItemType.Folder;
            _text = Type == WorkspaceItemType.Root ? "File Explorer" : Info.Name;
        }

        public WorkspaceItemType Type { get; }

        public FileSystemInfo Info { get; set; }

        public object SourceItem { get; }

        public bool HasItems { get; private set; }

        public IEnumerable Items
        {
            get
            {
                if (_innerItems.Count == 0)
                {
                    BuildInnerItems();
                }

                return _innerItems;
            }
        }

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

        public object ToolTipContent => null;

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
                return Type == WorkspaceItemType.Root ? KnownMonikers.RemoteFolder : Info.GetIcon(false);
            }
        }

        public ImageMoniker ExpandedIconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return Type == WorkspaceItemType.Root ? KnownMonikers.RemoteFolderOpen : Info.GetIcon(true);
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

        private void BuildInnerItems()
        {
            // First, clear out any existing items since this could be a refresh.
            foreach (WorkspaceItemNode item in _innerItems)
            {
                item.Dispose();
            }

            _innerItems.BeginBulkOperation();
            _innerItems.Clear();

            // Second, add the new items.
            if (Info is FileInfo file)
            {
                _innerItems.Add(new WorkspaceItemNode(this, file));
            }
            else if (Info is DirectoryInfo dir)
            {
                foreach (FileSystemInfo item in dir.EnumerateFileSystemInfos().OrderBy(i => i is FileInfo))
                {
                    _innerItems.Add(new WorkspaceItemNode(this, item));
                }
            }

            // Thirdly, hook up the file system watcher if this is a directory.
            if (Info is DirectoryInfo && _watcher == null)
            {
                _watcher = new FileSystemWatcher(Info.FullName);
                _watcher.Renamed += OnRenamed;
                _watcher.Deleted += OnDeleted;
                _watcher.Created += OnCreated;
                _watcher.EnableRaisingEvents = true;
            }

            // Lastly, register the updated items.
            _innerItems.EndBulkOperation();
            HasItems = _innerItems.Count > 0;
            RaisePropertyChanged(nameof(HasItems));
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            WorkspaceItemNode item = _innerItems.FirstOrDefault(i => e.OldFullPath == i.Info.FullName);

            if (item != null)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    item.Info = item.Info is FileInfo ? new FileInfo(e.FullPath) : new DirectoryInfo(e.FullPath);
                    item.Text = e.Name;

                    WorkspaceItemNode[] items = _innerItems.OrderBy(i => i.Text).OrderBy(i => i.Info is FileInfo).ToArray();
                    _innerItems.BeginBulkOperation();
                    _innerItems.Clear();
                    _innerItems.AddRange(items);
                    _innerItems.EndBulkOperation();
                }).FireAndForget();
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            WorkspaceItemNode item = _innerItems.FirstOrDefault(i => e.FullPath == i.Info.FullName);

            if (_innerItems.Contains(item))
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    item.IsCut = true;
                    _innerItems.Remove(item);
                    RaisePropertyChanged(nameof(HasItems));
                }).FireAndForget();
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            FileSystemInfo info = File.Exists(e.FullPath) ? new FileInfo(e.FullPath) : new DirectoryInfo(e.FullPath);

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _innerItems.BeginBulkOperation();
                _innerItems.Add(new WorkspaceItemNode(this, info));

                WorkspaceItemNode[] items = _innerItems.OrderBy(i => i.Text).OrderBy(i => i.Info is FileInfo).ToArray();

                _innerItems.Clear();
                _innerItems.AddRange(items);
                _innerItems.EndBulkOperation();

                RaisePropertyChanged(nameof(HasItems));
            }).FireAndForget();
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                if (_watcher != null)
                {
                    _watcher.Created -= OnCreated;
                    _watcher.Deleted -= OnDeleted;
                    _watcher.Renamed -= OnRenamed;
                    _watcher.Dispose();
                    _watcher = null;
                }

                if (_innerItems != null)
                {
                    foreach (WorkspaceItemNode item in _innerItems)
                    {
                        item.Dispose();
                    }
                }
            }

            IsDisposed = true;
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
            return Task.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                BuildInnerItems();
            });
        }

        public void CancelLoad()
        {

        }

        public DirectionalDropArea SupportedAreas => DirectionalDropArea.On;

        public void OnDragEnter(DirectionalDropArea dropArea, DragEventArgs e)
        {
            if (Info is DirectoryInfo && e.Data.GetDataPresent(typeof(WorkspaceItemNode)))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        public void OnDragOver(DirectionalDropArea dropArea, DragEventArgs e)
        {
            if (Info is DirectoryInfo && e.Data.GetDataPresent(typeof(WorkspaceItemNode)))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        public void OnDragLeave(DirectionalDropArea dropArea, DragEventArgs e)
        {
            if (Info is DirectoryInfo && e.Data.GetDataPresent(typeof(WorkspaceItemNode)))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        public void OnDrop(DirectionalDropArea dropArea, DragEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.Data.GetDataPresent(typeof(WorkspaceItemNode)))
            {
                var node = e.Data.GetData(typeof(WorkspaceItemNode)) as WorkspaceItemNode;
                DTE dte = VS.GetRequiredService<DTE, DTE>();

                if (dte.Solution.FindProjectItem(node.Info.FullName) != null)
                {
                    VS.MessageBox.ShowError("You cannot move a file that is part of a project");
                    return;
                }

                if (node?.Info is FileInfo file)
                {
                    file.MoveTo(Path.Combine(Info.FullName, file.Name));
                }
                else if (node?.Info is DirectoryInfo dir)
                {
                    dir.MoveTo(Path.Combine(Info.FullName, dir.Name));
                }

                e.Handled = true;
            }
        }
    }
}