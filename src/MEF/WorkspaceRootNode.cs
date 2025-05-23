using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using EnvDTE;
using MAB.DotIgnore;
using Microsoft.Extensions.FileSystemGlobbing;

namespace WorkspaceFiles
{
    internal class WorkspaceRootNode : IAttachedCollectionSource, INotifyPropertyChanged, IDisposable
    {
        private readonly ObservableCollection<WorkspaceItemNode> _innerItems = [];
        private readonly DTE _dte;
        private readonly string _solutionDir;
        private bool _disposed = false;
        private List<DirectoryInfo> _directories;
        private Microsoft.Extensions.FileSystemGlobbing.Matcher _matcher;

        public WorkspaceRootNode(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _dte = dte;
            _solutionDir = Path.GetDirectoryName(_dte?.Solution?.FullName ?? "")?.TrimEnd('\\') + '\\';

            General.Saved += OnSettingsSaved;
            AddFolderCommand.AddFolderRequest += OnAddFolderRequested;
            RemoveFolderCommand.RemoveFolderRequest += OnRemoveFolderRequested;

            SetupGlobbingMatcher();
            UpdateDirectories();
            _dte = dte;
        }

        private void OnRemoveFolderRequested(object sender, string directory)
        {
            if (_directories.Any(d => d.FullName == directory))
            {
                var info = new DirectoryInfo(directory);
                _ = _directories.RemoveAll(d => d.FullName == directory);
                BuildInnerItems();
                PersistDirectoriesAsync().FireAndForget();
            }
        }

        private void OnAddFolderRequested(object sender, string directory)
        {
            var info = new DirectoryInfo(directory.TrimEnd('\\') + '\\');

            if (!_directories.Contains(info))
            {
                _directories.Add(info);
                BuildInnerItems();
                PersistDirectoriesAsync().FireAndForget();
            }
        }

        private void OnSettingsSaved(General general)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SetupGlobbingMatcher();
            UpdateDirectories();
            BuildInnerItems();
        }

        public object SourceItem => this;

        public bool HasItems => General.Instance.Enabled || _directories?.Any() == true;

        public IEnumerable Items
        {
            get
            {
                if (!string.IsNullOrEmpty(_solutionDir) && _innerItems.Count == 0)
                {
                    BuildInnerItems();
                }

                return _innerItems;
            }
        }

        private void BuildInnerItems()
        {
            _innerItems.Clear();

            foreach (DirectoryInfo dir in _directories)
            {
                // Skip duplicates
                if (_innerItems.FirstOrDefault((node) => node.Info.FullName == dir.FullName) != null)
                {
                    continue;
                }

                IgnoreList ignoreList = GetIgnore(dir.FullName);
                _innerItems.Add(new WorkspaceItemNode(this, dir, ignoreList, _matcher));
            }

            RaisePropertyChanged(nameof(HasItems));
        }

        private void UpdateDirectories()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _directories = [];

            if (!General.Instance.Enabled || string.IsNullOrEmpty(_solutionDir))
            {
                return;
            }

            if (!_dte.Solution.Globals.VariableExists["FileExplorer"])
            {
                if (TryGetGitRepoRoot(out DirectoryInfo dir))
                {
                    _directories.Add(dir);
                }
                else
                {
                    _directories.Add(new(_solutionDir));
                }
            }
            else
            {
                var dirs = _dte.Solution.Globals["FileExplorer"].ToString().Split('|');

                foreach (var dir in dirs)
                {
                    try
                    {
                        var info = new DirectoryInfo(Path.Combine(_solutionDir, dir));
                        if (info.Exists)
                        {
                            _directories.Add(info);
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Log();
                    }
                }
            }
        }

        private void SetupGlobbingMatcher()
        {
            var ignoreFilePatterns = General.Instance.IgnoreList.Split(';').Select(i => i.Trim());
            _matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher(StringComparison.OrdinalIgnoreCase);
            _matcher.AddIncludePatterns(ignoreFilePatterns);
        }

        private async Task PersistDirectoriesAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var raw = string.Join("|", _directories.Select(d => PackageUtilities.MakeRelative(_solutionDir, d.FullName)));
                _dte.Solution.Globals["FileExplorer"] = raw;
                _dte.Solution.Globals.VariablePersists["FileExplorer"] = _directories.Any();
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private bool TryGetGitRepoRoot(out DirectoryInfo solRoot)
        {
            solRoot = null;

            if (string.IsNullOrEmpty(_solutionDir))
            {
                return false;
            }

            DirectoryInfo currentRoot = new(_solutionDir);

            if (currentRoot == null)
            {
                return false;
            }

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

            return false;
        }

        private IgnoreList GetIgnore(string root)
        {
            var info = new DirectoryInfo(root);

            do
            {
                var ignoreFile = Path.Combine(info.FullName, ".gitignore");

                if (File.Exists(ignoreFile))
                {
                    return new IgnoreList(ignoreFile);
                }

                info = info.Parent;

            } while (info != null);

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
