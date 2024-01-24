using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace WorkspaceFiles
{
    internal class WorkspaceRootNode : IAttachedCollectionSource, INotifyPropertyChanged, IDisposable
    {
        private readonly ObservableCollection<WorkspaceItemNode> _innerItems = [];
        private bool _disposed = false;
        private readonly DirectoryInfo _info;
        private readonly DateTime _lastUpdated; // used for debouncing purposes.

        public WorkspaceRootNode(DirectoryInfo info)
        {
            _info = info;
            General.Saved += OnSettingsSaved;
        }

        private void OnSettingsSaved(General general)
        {
            if (DateTime.Now > _lastUpdated.AddSeconds(1)) // debounce
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    BuildInnerItems();
                }).FireAndForget();
            }
        }

        public object SourceItem => this;

        public bool HasItems => General.Instance.Enabled;

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

        private void BuildInnerItems()
        {
            if (General.Instance.Enabled)
            {
                _innerItems.Clear();
                _innerItems.Add(new WorkspaceItemNode(this, _info));
                RaisePropertyChanged(nameof(HasItems));
            }
            else if (_innerItems.Count > 0)
            {
                DisposeChildren();
                RaisePropertyChanged(nameof(HasItems));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                General.Saved -= OnSettingsSaved;
                DisposeChildren();
            }

            _disposed = true;
        }

        public void DisposeChildren()
        {
            if (_innerItems != null)
            {
                foreach (WorkspaceItemNode child in _innerItems)
                {
                    child.Dispose();
                }

                _innerItems.Clear();
            }
        }
    }
}
