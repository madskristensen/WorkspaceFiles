using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace WorkspaceFiles
{
    internal class WorkspaceItemSource : IAsyncAttachedCollectionSource
    {
        private readonly FileSystemInfo _info;
        private List<WorkspaceItem> _childItems = [];

        public WorkspaceItemSource(object item, FileSystemInfo info)
        {
            _info = info;
            SourceItem = item;
            IsUpdatingHasItems = _info is not FileInfo;

            // Sync build items
            if (item == null || (item is WorkspaceItem workspaceItem && workspaceItem.Type == WorkspaceItemType.Root))
            {
                BuildChildItems();
            }
            // Async build items
            else if (IsUpdatingHasItems)
            {
                ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                {
                    await TaskScheduler.Default;
                    BuildChildItems();
                }, VsTaskRunContext.UIThreadIdlePriority).FireAndForget();
            }
        }

        public object SourceItem { get; }

        public bool HasItems => _childItems?.Count > 0;

        public bool IsUpdatingHasItems { get; private set; }

        private void BuildChildItems()
        {
            _childItems = [];

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasItems)));
        }

        public IEnumerable Items => _childItems;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}