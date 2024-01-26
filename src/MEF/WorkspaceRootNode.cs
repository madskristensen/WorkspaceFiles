using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using MAB.DotIgnore;

namespace WorkspaceFiles
{
    internal class WorkspaceRootNode : IAttachedCollectionSource, INotifyPropertyChanged, IDisposable
    {
        private readonly ObservableCollection<WorkspaceItemNode> _innerItems = [];
        private bool _disposed = false;
        private IgnoreList _ignoreList;
        private readonly DirectoryInfo _info;

        public WorkspaceRootNode(DirectoryInfo info)
        {
            _info = info;
            General.Saved += OnSettingsSaved;

            _ignoreList = GetIgnore(info.FullName);
        }

        private void OnSettingsSaved(General general)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                BuildInnerItems();
            }).FireAndForget();
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
                _innerItems.Add(new WorkspaceItemNode(this, _info, _ignoreList));
                RaisePropertyChanged(nameof(HasItems));
            }
            else if (_innerItems.Count > 0)
            {
                DisposeChildren();
                RaisePropertyChanged(nameof(HasItems));
            }
        }

        private IgnoreList GetIgnore(string root)
        {
            string ignoreFile = Path.Combine(root, ".gitignore");

            if (File.Exists(ignoreFile))
            {
                return new IgnoreList(ignoreFile);
            }

            return null;
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
