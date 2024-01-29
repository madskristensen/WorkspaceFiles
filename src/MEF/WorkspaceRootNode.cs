using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using EnvDTE;
using MAB.DotIgnore;

namespace WorkspaceFiles
{
    internal class WorkspaceRootNode : IAttachedCollectionSource, INotifyPropertyChanged, IDisposable
    {
        private readonly ObservableCollection<WorkspaceItemNode> _innerItems = [];
        private bool _disposed = false;
        private List<DirectoryInfo> _directories;

        public WorkspaceRootNode()
        {
            General.Saved += OnSettingsSaved;
            AddFolderCommand.AddFolderRequest += OnAddFolderRequested;
            RemoveFolderCommand.RemoveFolderRequest += OnRemoveFolderRequested;
        }

        private void OnRemoveFolderRequested(object sender, string directory)
        {
            if (_directories.Any(d => d.FullName == directory))
            {
                var info = new DirectoryInfo(directory);
                _directories.RemoveAll(d => d.FullName == directory);
                BuildAllDirectories();
                PersistDirectoriesAsync().FireAndForget();
            }
        }

        private void OnAddFolderRequested(object sender, string directory)
        {
            var info = new DirectoryInfo(directory);

            if (!_directories.Contains(info))
            {
                _directories.Add(info);
                BuildAllDirectories();
                PersistDirectoriesAsync().FireAndForget();
            }
        }

        private void OnSettingsSaved(General general)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                UpdateDirectories();
                BuildAllDirectories();
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
                    UpdateDirectories();
                    BuildAllDirectories();
                }

                return _innerItems;
            }
        }

        private void BuildAllDirectories()
        {
            _innerItems.Clear();

            foreach (DirectoryInfo dir in _directories)
            {
                IgnoreList ignoreList = GetIgnore(dir.FullName);
                _innerItems.Add(new WorkspaceItemNode(this, dir, ignoreList));
            }

            RaisePropertyChanged(nameof(HasItems));
        }

        private void UpdateDirectories()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DTE dte = VS.GetRequiredService<DTE, DTE>();

            _directories = [];

            if (!dte.Solution.Globals.VariableExists["FileExplorer"])
            {
                if (General.Instance.Enabled && TryGetRoot(out DirectoryInfo dir))
                {
                    _directories.Add(dir);
                }
            }
            else
            {
                var dirs = dte.Solution.Globals["FileExplorer"].ToString().Split('|');
                var slnFolder = Path.GetDirectoryName(dte.Solution.FullName);

                foreach (var dir in dirs)
                {
                    try
                    {
                        var info = new DirectoryInfo(Path.Combine(slnFolder, dir));
                        if (info.Exists)
                        {
                            _directories.Add(info);
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.LogAsync().FireAndForget();
                    }
                }
            }
        }

        private async Task PersistDirectoriesAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var raw = string.Join("|", _directories.Select(d => d.FullName));
                DTE dte = await VS.GetRequiredServiceAsync<DTE, DTE>();
                dte.Solution.Globals["FileExplorer"] = raw;
                dte.Solution.Globals.VariablePersists["FileExplorer"] = true;
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private static bool TryGetRoot(out DirectoryInfo solRoot)
        {
            solRoot = null;
            Community.VisualStudio.Toolkit.Solution solution = VS.Solutions.GetCurrentSolution();

            if (string.IsNullOrEmpty(solution?.FullPath))
            {
                return false;
            }

            solRoot = new DirectoryInfo(Path.GetDirectoryName(solution.FullPath));
            DirectoryInfo currentRoot = new(solRoot.FullName);

            while (currentRoot != null)
            {
                var dotGit = Path.Combine(currentRoot.FullName, ".git");

                if (Directory.Exists(dotGit))
                {
                    solRoot = currentRoot;
                    return true;
                }

                currentRoot = currentRoot.Parent;
            }

            return true;
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
                AddFolderCommand.AddFolderRequest -= OnAddFolderRequested;
                RemoveFolderCommand.RemoveFolderRequest -= OnRemoveFolderRequested;
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
