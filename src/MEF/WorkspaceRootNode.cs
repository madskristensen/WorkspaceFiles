using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using EnvDTE;
using MAB.DotIgnore;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

namespace WorkspaceFiles
{
    internal class WorkspaceRootNode : IAttachedCollectionSource, ITreeDisplayItem, ITreeDisplayItemWithImages, IInteractionPatternProvider, IPrioritizedComparable, IBrowsablePattern, INotifyPropertyChanged, IDisposable, IRefreshPattern
    {
        private readonly ObservableCollection<WorkspaceItemNode> _innerItems = [];
        private readonly DTE _dte;
        private readonly string _solutionDir;
        private readonly IVsHierarchyItem _solutionHierarchyItem;
        private bool _disposed = false;
        private List<DirectoryInfo> _directories;
        private Microsoft.Extensions.FileSystemGlobbing.Matcher _matcher;

        /// <summary>
        /// The ContainedBy collection for this item, set during search.
        /// </summary>
        private IAttachedCollectionSource _containedByCollection;

        // Cache for Git repository root to avoid repeated directory traversals
        private static readonly ConcurrentDictionary<string, DirectoryInfo> _gitRootCache = new();

        // Cache for .gitignore files to avoid repeated file system access
        private static readonly ConcurrentDictionary<string, IgnoreList> _gitIgnoreCache = new();

        // Cache for directories that don't have .gitignore files (negative cache)
        private static readonly ConcurrentDictionary<string, bool> _noGitIgnoreCache = new();

        public WorkspaceRootNode(DTE dte, IVsHierarchyItem solutionHierarchyItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _dte = dte;
            _solutionHierarchyItem = solutionHierarchyItem;
            _solutionDir = Path.GetDirectoryName(_dte?.Solution?.FullName ?? "")?.TrimEnd('\\') + '\\';

            General.Saved += OnSettingsSaved;
            AddFolderCommand.AddFolderRequest += OnAddFolderRequested;
            RemoveFolderCommand.RemoveFolderRequest += OnRemoveFolderRequested;

            SetupGlobbingMatcher();
            UpdateDirectories();
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

        #region ITreeDisplayItem and ITreeDisplayItemWithImages

        /// <summary>
        /// Display text for the root node in search results.
        /// </summary>
        public string Text => "Workspace Files";

        public string ToolTipText => "Files and folders from the workspace";

        public object ToolTipContent => null;

        public FontWeight FontWeight => FontWeights.Normal;

        public System.Windows.FontStyle FontStyle => FontStyles.Normal;

        public bool IsCut => false;

        public ImageMoniker IconMoniker => KnownMonikers.LinkedFolderOpened;

        public ImageMoniker ExpandedIconMoniker => KnownMonikers.LinkedFolderOpened;

        public ImageMoniker OverlayIconMoniker => default;

        public ImageMoniker StateIconMoniker => default;

        public string StateToolTipText => string.Empty;

        #endregion

        #region IInteractionPatternProvider and IPrioritizedComparable

        private static readonly HashSet<Type> _supportedPatterns =
        [
            typeof(ITreeDisplayItem),
            typeof(ITreeDisplayItemWithImages),
            typeof(IBrowsablePattern),
        ];

        public TPattern GetPattern<TPattern>() where TPattern : class
        {
            if (_supportedPatterns.Contains(typeof(TPattern)))
            {
                return this as TPattern;
            }
            return null;
        }

        public int Priority => 0;

        public int CompareTo(object obj)
        {
            // WorkspaceRootNode should always appear at the end of solution children
            return 1;
        }

        #endregion

        #region IBrowsablePattern

        public object GetBrowseObject() => null;

        #endregion

        /// <summary>
        /// Gets the parent item (solution hierarchy item) for ContainedBy relationship support.
        /// This is used during search to trace this node back to the solution.
        /// </summary>
        public object ParentItem => _solutionHierarchyItem;

        /// <summary>
        /// Gets this item as the source for the attached collection (IAttachedCollectionSource).
        /// For IAttachedCollectionSource semantics, SourceItem should be the item itself.
        /// </summary>
        public object SourceItem => this;

        /// <summary>
        /// Gets or sets the ContainedBy collection for this item.
        /// This is set during search to enable Solution Explorer to trace items back to their parents.
        /// </summary>
        public IAttachedCollectionSource ContainedByCollection
        {
            get => _containedByCollection;
            set => _containedByCollection = value;
        }

        /// <summary>
        /// Gets the configured workspace directories for search enumeration.
        /// </summary>
        public IReadOnlyList<DirectoryInfo> Directories => _directories;

        /// <summary>
        /// Gets the solution hierarchy item for search results.
        /// </summary>
        public IVsHierarchyItem SolutionHierarchyItem => _solutionHierarchyItem;

        /// <summary>
        /// Gets the globbing matcher for filtering files.
        /// </summary>
        public Microsoft.Extensions.FileSystemGlobbing.Matcher GlobbingMatcher => _matcher;

        public bool HasItems => General.Instance.Enabled && _directories?.Count > 0;

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

            if (_directories == null)
            {
                return;
            }

            // Use HashSet for faster duplicate checking instead of FirstOrDefault
            var existingPaths = new HashSet<string>();

            foreach (DirectoryInfo dir in _directories)
            {
                // Skip duplicates more efficiently
                if (!existingPaths.Add(dir.FullName))
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
            // Optimize pattern setup by avoiding unnecessary LINQ operations
            var ignoreFilePatterns = General.Instance.IgnoreList.Split(';');
            _matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher(StringComparison.OrdinalIgnoreCase);

            for (var i = 0; i < ignoreFilePatterns.Length; i++)
            {
                var pattern = ignoreFilePatterns[i].Trim();
                if (!string.IsNullOrEmpty(pattern))
                {
                    _ = _matcher.AddInclude(pattern);
                }
            }
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

            // Check cache first to avoid repeated directory traversals
            if (_gitRootCache.TryGetValue(_solutionDir, out solRoot))
            {
                return solRoot != null;
            }

            DirectoryInfo currentRoot = new(_solutionDir);

            if (currentRoot == null)
            {
                _gitRootCache[_solutionDir] = null;
                return false;
            }

            // Limit traversal depth to avoid excessive I/O on systems without git repos
            // Most git repos are within 10 levels of the solution directory
            const int maxDepth = 10;
            var depth = 0;

            while (currentRoot != null && depth < maxDepth)
            {
                var dotGit = Path.Combine(currentRoot.FullName, ".git");

                if (Directory.Exists(dotGit))
                {
                    solRoot = currentRoot;
                    // Cache the result for future use
                    _gitRootCache[_solutionDir] = solRoot;
                    return true;
                }

                currentRoot = currentRoot.Parent;
                depth++;
            }

            // Cache negative result as well
            _gitRootCache[_solutionDir] = null;
            return false;
        }

        private IgnoreList GetIgnore(string root)
        {
            // Check negative cache first
            if (_noGitIgnoreCache.ContainsKey(root))
            {
                return null;
            }

            var info = new DirectoryInfo(root);

            // Limit traversal depth to avoid excessive I/O
            const int maxDepth = 10;
            var depth = 0;

            while (info != null && depth < maxDepth)
            {
                var ignoreFile = Path.Combine(info.FullName, ".gitignore");

                // Check cache first before file system access
                if (_gitIgnoreCache.TryGetValue(ignoreFile, out IgnoreList cachedIgnoreList))
                {
                    return cachedIgnoreList;
                }

                if (File.Exists(ignoreFile))
                {
                    try
                    {
                        var ignoreList = new IgnoreList(ignoreFile);
                        // Cache the result for future use
                        _gitIgnoreCache[ignoreFile] = ignoreList;
                        return ignoreList;
                    }
                    catch
                    {
                        // If we can't read the file, cache it as non-existent
                        _noGitIgnoreCache[root] = true;
                        return null;
                    }
                }

                info = info.Parent;
                depth++;
            }

            // Cache negative result
            _noGitIgnoreCache[root] = true;
            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Task RefreshAsync()
        {
            // The root node's RefreshAsync is called when the user clicks the Solution Explorer
            // refresh button. We intentionally do NOT cascade refresh calls to children because:
            // 1. Each child WorkspaceItemNode has its own FileSystemWatcher that handles changes
            // 2. Cascading RefreshAsync calls causes children to rebuild their _children collections,
            //    which can incorrectly change HasItems values and collapse tree nodes
            //
            // The root node only needs to handle structural changes to its immediate children
            // (root folders being added/removed), which is handled by OnAddFolderRequested,
            // OnRemoveFolderRequested, and the child FileSystemWatchers.

            return Task.CompletedTask;
        }

        public void CancelLoad()
        {
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true; // Set early to prevent race conditions
                General.Saved -= OnSettingsSaved;
                AddFolderCommand.AddFolderRequest -= OnAddFolderRequested;
                RemoveFolderCommand.RemoveFolderRequest -= OnRemoveFolderRequested;
                DisposeChildren();
                ClearCaches();
            }
        }

        /// <summary>
        /// Clears the static caches to prevent memory leaks when solutions are closed.
        /// Only clears entries related to the current solution directory.
        /// </summary>
        private void ClearCaches()
        {
            if (string.IsNullOrEmpty(_solutionDir))
            {
                return;
            }

            // Clear git root cache entries for this solution
            _ = _gitRootCache.TryRemove(_solutionDir, out _);

            // Clear gitignore caches for paths under this solution directory
            foreach (var key in _gitIgnoreCache.Keys)
            {
                if (key.StartsWith(_solutionDir, StringComparison.OrdinalIgnoreCase))
                {
                    _ = _gitIgnoreCache.TryRemove(key, out _);
                }
            }

            foreach (var key in _noGitIgnoreCache.Keys)
            {
                if (key.StartsWith(_solutionDir, StringComparison.OrdinalIgnoreCase))
                {
                    _ = _noGitIgnoreCache.TryRemove(key, out _);
                }
            }
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
