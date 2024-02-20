using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private readonly BulkObservableCollection<WorkspaceItemNode> _innerItems = [];
        private string _text;
        private bool _isCut;
        private FileSystemWatcher _watcher;
        private bool _isDisposed;
        private readonly IgnoreList _ignoreList;
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
            }
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
                _innerItems.Add(new WorkspaceItemNode(this, file, _ignoreList));
            }
            else if (Info is DirectoryInfo dir)
            {
                foreach (FileSystemInfo item in dir.EnumerateFileSystemInfos().OrderBy(i => i is FileInfo))
                {
                    if (item.Name != ".git") // ignore .git folder for safety reasons
                    {
                        _innerItems.Add(new WorkspaceItemNode(this, item, _ignoreList));
                    }
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

        /// <summary>
        /// Sort items in the same way as the file system.
        /// Directories first, then files, and then sorted by name. 
        /// </summary>
        void SortAsFileSystem(BulkObservableCollection<WorkspaceItemNode> collection)
        {
            // Workaround since the BulkObservableCollection does not support sorting.
            WorkspaceItemNode[] tmp = collection.OrderBy(i => i.Text).OrderBy(i => i.Info is FileInfo).ToArray();
            _innerItems.Clear();
            _innerItems.AddRange(tmp);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (e.Name.Contains('~') || e.Name.IndexOf(".tmp", StringComparison.OrdinalIgnoreCase) > -1) // temp files created by VS and deleted immediately after
            {
                return;
            }

            WorkspaceItemNode item;
            // Lock the items collection since it can be modified by another running thread at the same time that we received this event.
            lock (_innerItems)
            {
                item = _innerItems.FirstOrDefault(i => e.OldFullPath == i.Info.FullName);
            }

            if (item != null)
            {
                // Update the existing item with the new name and path.
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    item.Info = item.Info is FileInfo ? new FileInfo(e.FullPath) : new DirectoryInfo(e.FullPath);
                    item.Text = e.Name;

                    lock (_innerItems)
                    {
                        _innerItems.BeginBulkOperation();
                        SortAsFileSystem(_innerItems);
                        _innerItems.EndBulkOperation();
                    }
                }).FireAndForget();
            }
            else
            {
                // Handling cases where VS temp files are renamed to the actual file name. Cases like this happens when a file is modified and saved.
                // In this case the OnDeleted event is fired for the old file and the OnRenamed event is fired for the temp file.
                // Since the OnDeleted event can be fired first, the item is removed from the collection and the OnRenamed event is ignored.
                FileSystemInfo info = File.Exists(e.FullPath) ? new FileInfo(e.FullPath) : new DirectoryInfo(e.FullPath);

                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await CreateItemAsync(info);
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
                    
                    lock (_innerItems)
                    {
                        _innerItems.Remove(item);
                    }

                    RaisePropertyChanged(nameof(HasItems));
                }).FireAndForget();
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (e.Name.Contains('~') || e.Name.IndexOf(".tmp", StringComparison.OrdinalIgnoreCase) > -1) // temp files created by VS and deleted immediately after
            {
                return;
            }

            FileSystemInfo info = File.Exists(e.FullPath) ? new FileInfo(e.FullPath) : new DirectoryInfo(e.FullPath);

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await CreateItemAsync(info);
            }).FireAndForget();
        }

        private async Task CreateItemAsync(FileSystemInfo info)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            lock (_innerItems)
            {
                // Check if the item already exists in the collection.
                // Since multiple save file events can be fired in quick succession, the item might already exist when being
                // moved from a temp file to the actual file.
                WorkspaceItemNode item = _innerItems.FirstOrDefault(node => node.Info.FullName == info.FullName);
                if (item != null)
                {
                    return;
                }

                _innerItems.BeginBulkOperation();
                _innerItems.Add(new WorkspaceItemNode(this, info, _ignoreList));
                SortAsFileSystem(_innerItems);
                _innerItems.EndBulkOperation();
            }

            RaisePropertyChanged(nameof(HasItems));
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