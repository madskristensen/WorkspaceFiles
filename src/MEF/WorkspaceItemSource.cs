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
        private readonly FileSystemInfo _info;
        private readonly List<WorkspaceItem> _childItems = [];

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
                foreach (FileSystemInfo item in dir.EnumerateFileSystemInfos().OrderBy(f => f is FileInfo))
                {
                    _childItems.Add(new WorkspaceItem(item));
                }
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
        }

        public void AfterCollapse()
        {
            foreach (WorkspaceItem item in _childItems.ToArray())
            {
                if (!File.Exists(item.Info.FullName) && !Directory.Exists(item.Info.FullName))
                {
                    item.Info.Refresh();
                    item.Dispose();
                }
            }
        }

        public IEnumerable Items => _childItems;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}