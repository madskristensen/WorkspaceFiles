using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    internal class WorkspaceItemSource : IAsyncAttachedCollectionSource, ISupportExpansionEvents
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly FileSystemInfo _info;
        private readonly List<WorkspaceItem> _childItems = [];
        private FileSystemWatcher _watcher;

        public WorkspaceItemSource(object item, FileSystemInfo info)
        {
            _info = info;
            SourceItem = item;
            HasItems = _info is not FileInfo;
            IsUpdatingHasItems = _info is not FileInfo;

            // Sync build items
            if (item == null || (item is WorkspaceItem workspaceItem && workspaceItem.Type == WorkspaceItemType.Root))
            {
                BuildChildItems();
            }
        }

        public object SourceItem { get; }

        public bool HasItems { get; private set; }

        public bool IsUpdatingHasItems { get; private set; }

        public IEnumerable Items => _childItems;

        private void BuildChildItems()
        {
            _childItems.Clear();

            if (SourceItem == null)
            {
                _childItems.Add(new WorkspaceItem(_info, isRoot: true));
            }
            else if (_info is FileInfo file)
            {
                _childItems.Add(new WorkspaceItem(file));
            }
            else if (_info is DirectoryInfo dir)
            {
                _childItems.AddRange(dir.EnumerateFileSystemInfos().OrderBy(f => f is FileInfo).Select(i => new WorkspaceItem(i)));
            }

            IsUpdatingHasItems = false;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdatingHasItems)));

            HasItems = _childItems.Any();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasItems)));
        }

        public void BeforeExpand()
        {
            IsUpdatingHasItems = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdatingHasItems)));
            BuildChildItems();

            _watcher = new FileSystemWatcher(_info.FullName)
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _watcher.Renamed += _watcher_Renamed;
            _watcher.Deleted += _watcher_Deleted;
            _watcher.Created += _watcher_Created;
        }

        public void AfterCollapse()
        {
            //_watcher.Renamed -= _watcher_Renamed;
            //_watcher.Deleted -= _watcher_Deleted;
            //_watcher.Created -= _watcher_Created;
            //_watcher.Dispose();
        }

        private void _watcher_Renamed(object sender, RenamedEventArgs e)
        {
            WorkspaceItem item = _childItems.FirstOrDefault(i => e.OldFullPath == i.Info.FullName);

            if (item != null)
            {
                if (item.Info is FileInfo)
                {
                    item.Info = new FileInfo(e.FullPath);
                }
                else if (item.Info is DirectoryInfo)
                {
                    item.Info = new DirectoryInfo(e.FullPath);
                }

                item.Text = e.Name;
            }
        }

        private void _watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            WorkspaceItem item = _childItems.FirstOrDefault(i => e.FullPath == i.Info.FullName);

            if (item != null)
            {
                if (!File.Exists(item.Info.FullName) && !Directory.Exists(item.Info.FullName))
                {
                    item.Info.Refresh();
                    //item.Dispose();
                    item.IsCut = true;
                }
            }
        }

        private void _watcher_Created(object sender, FileSystemEventArgs e)
        {
            WorkspaceItem item = _childItems.FirstOrDefault(i => e.FullPath == i.Info.FullName);

            if (item != null)
            {
                item.Info.Refresh();
                item.IsCut = false;
            }
        }
    }
}