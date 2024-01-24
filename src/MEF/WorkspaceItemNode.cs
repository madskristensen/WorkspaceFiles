using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

namespace WorkspaceFiles
{
    [DebuggerDisplay("{Text}")]
    internal class WorkspaceItemNode :
        IAttachedCollectionSource,
        ISupportExpansionEvents,
        ITreeDisplayItem,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IBrowsablePattern,
        IInteractionPatternProvider,
        IContextMenuPattern,
        IInvocationPattern,
        ISupportDisposalNotification,
        IDisposable
    {
        private BulkObservableCollection<WorkspaceItemNode> _innerItems;
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
            typeof(ISupportExpansionEvents),
            typeof(ISupportDisposalNotification),
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
                if (_innerItems == null)
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

        public IContextMenuController ContextMenuController => new WorkspaceItemContextMenuController();

        public bool CanPreview => Info is FileInfo;

        public IInvocationController InvocationController => new WorkspaceItemInvocationController();

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
            _innerItems = [];
            _innerItems.BeginBulkOperation();

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

            _innerItems.EndBulkOperation();
            HasItems = _innerItems.Count > 0;
            RaisePropertyChanged(nameof(HasItems));
        }

        public void BeforeExpand()
        {
            if (Info is DirectoryInfo && _watcher == null)
            {
                _watcher = new FileSystemWatcher(Info.FullName);
                _watcher.Renamed += OnRenamed;
                _watcher.Deleted += OnDeleted;
                _watcher.Created += OnCreated;
                _watcher.EnableRaisingEvents = true;
            }
        }

        public void AfterCollapse()
        {
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
    }
}