using System.Collections;
using System.Collections.Generic;
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
        private readonly List<IDirectoryProvider> _providers = [new LocalSolutionProvider()];

        public WorkspaceRootNode()
        {
            General.Saved += OnSettingsSaved;

            foreach (IDirectoryProvider provider in _providers)
            {
                provider.DirectoryChanged += OnDirectoryChanged;
            }
        }

        private void OnDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            BuildAllProvider();
        }

        private void OnSettingsSaved(General general)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                BuildAllProvider();
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
                    BuildAllProvider();
                }

                return _innerItems;
            }
        }

        private void BuildAllProvider()
        {
            _innerItems.Clear();

            foreach (IDirectoryProvider item in _providers)
            {
                foreach (DirectoryInfo dir in item.GetDirectories())
                {
                    var ignoreList = GetIgnore(dir.FullName);

                    
                    _innerItems.Add(new WorkspaceItemNode(this, dir, ignoreList));
                }
            }

                    RaisePropertyChanged(nameof(HasItems));
        }

        private IgnoreList GetIgnore(string root)
        {
            var ignoreFile = Path.Combine(root, ".gitignore");

            return File.Exists(ignoreFile) ? new IgnoreList(ignoreFile) : null;
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

                foreach (IDirectoryProvider provider in _providers)
                {
                    provider.DirectoryChanged -= OnDirectoryChanged;
                    provider.Dispose();
                }
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
