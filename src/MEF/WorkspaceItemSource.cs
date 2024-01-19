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
        private List<WorkspaceItem> _childItems = new();

        public WorkspaceItemSource(object item, FileSystemInfo info)
        {
            _info = info;
            SourceItem = item;
            HasItems = item == null; // workspace node
            IsUpdatingHasItems = !HasItems && _info is not FileInfo;

            // Sync build items
            if (HasItems)
            {
                BuildChildItems();
            }
            // Async build items
            else if (IsUpdatingHasItems)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    BuildChildItems();
                }).FireAndForget();
            }
        }

        public object SourceItem { get; }

        public bool HasItems { get; private set; }

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
                foreach (FileSystemInfo item in dir.GetDirectories())
                {
                    _childItems.Add(new WorkspaceItem(item));
                }

                foreach (FileSystemInfo item in dir.GetFiles())
                {
                    _childItems.Add(new WorkspaceItem(item));
                }
            }

            IsUpdatingHasItems = false;
            HasItems = _childItems.Any();

            if (HasItems)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HasItems"));
            }
        }

        public IEnumerable Items => _childItems;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}