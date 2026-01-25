using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EnvDTE;
using MAB.DotIgnore;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Threading;
using WorkspaceFiles.MEF;
using FontStyle = System.Windows.FontStyle;

namespace WorkspaceFiles
{
    [DebuggerDisplay("{Text}")]
    internal class WorkspaceItemNode :
        IAttachedCollectionSource,
        ITreeDisplayItem,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IBrowsablePattern,
        IInteractionPatternProvider,
        IContextMenuPattern,
        IInvocationPattern,
        ISupportDisposalNotification,
        IDisposable,
        IRefreshPattern,
        IDragDropSourcePattern,
        IDragDropTargetPattern
    {
        private readonly FileSystemWatcher _watcher;
        private readonly IgnoreList _ignoreList;
        private readonly Microsoft.Extensions.FileSystemGlobbing.Matcher _globbingMatcher;
        private BulkObservableCollection<WorkspaceItemNode> _children;

        private static readonly HashSet<Type> _supportedPatterns =
        [
            typeof(ITreeDisplayItem),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(IInvocationPattern),
            typeof(ISupportDisposalNotification),
            typeof(IRefreshPattern),
            typeof(IDragDropSourcePattern),
            typeof(IDragDropTargetPattern),
        ];

        public WorkspaceItemNode(object parent, FileSystemInfo info, IgnoreList ignoreList, Microsoft.Extensions.FileSystemGlobbing.Matcher globbingMatcher)
        {
            _ignoreList = ignoreList;
            _globbingMatcher = globbingMatcher;

            Info = info;
            SourceItem = parent;
            Type = parent is WorkspaceRootNode ? WorkspaceItemType.Root : info is FileInfo ? WorkspaceItemType.File : WorkspaceItemType.Folder;

            SetIsCut();

            if (info is DirectoryInfo dir)
            {
                try
                {
                    HasItems = dir.EnumerateFileSystemInfos().Any();
                }
                catch (Exception)
                {
                    // Directory might not be accessible, default to showing expander
                    HasItems = true;
                }

                _watcher = new FileSystemWatcher(info.FullName)
                {
                    // Performance optimizations for file system watcher
                    IncludeSubdirectories = false, // Only watch immediate children
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                    InternalBufferSize = 32768 // Increase buffer size to handle rapid changes
                };
                _watcher.Renamed += OnFileSystemChanged;
                _watcher.Deleted += OnFileSystemChanged;
                _watcher.Created += OnFileSystemChanged;
                _watcher.Error += OnWatcherError;
                _watcher.EnableRaisingEvents = true;
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            // FileSystemWatcher can stop raising events after an error
            // Attempt to restart it
            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception)
            {
                // Watcher may be in an unrecoverable state, log and continue
            }
        }

        public WorkspaceItemType Type { get; }
        public FileSystemInfo Info { get; set; }
        public object SourceItem { get; }
        public string Text => Info?.Name;
        public string ToolTipText => "";
        public string StateToolTipText => "";
        public object ToolTipContent => WorkspaceItemNodeTooltip.GetTooltip(this);
        public FontWeight FontWeight => FontWeights.Normal;
        public FontStyle FontStyle => FontStyles.Normal;
        public bool IsCut { get; private set; }
        public int Priority => 0;
        public bool CanPreview => Info is FileInfo;
        public ImageMoniker OverlayIconMoniker => default;
        public ImageMoniker StateIconMoniker => default;
        public bool HasItems { get; private set; }

        public IEnumerable Items
        {
            get
            {
                if (_children == null)
                {
                    RefreshChildren();
                }

                return _children;
            }
        }

        public ImageMoniker IconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return Type == WorkspaceItemType.Root ? KnownMonikers.LinkedFolderClosed : Info.GetIcon(false);
            }
        }

        public ImageMoniker ExpandedIconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return Type == WorkspaceItemType.Root ? KnownMonikers.LinkedFolderOpened : Info.GetIcon(true);
            }
        }

        // PERFORMANCE OPTIMIZATION: Cache controller instances to avoid repeated allocations
        private static readonly WorkspaceItemContextMenuController _contextMenuController = new();
        private static readonly WorkspaceItemInvocationController _invocationController = new();
        private static readonly WorkspaceItemNodeDragDropSourceController _dragDropSourceController = new();

        public IContextMenuController ContextMenuController => _contextMenuController;
        public IInvocationController InvocationController => _invocationController;
        public IDragDropSourceController DragDropSourceController => _dragDropSourceController;

        public bool IsDisposed
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    RaisePropertyChanged(nameof(IsDisposed));
                }
            }
        }

        private void SetIsCut()
        {
            if (Type != WorkspaceItemType.Root && _ignoreList != null)
            {
                if (Info is DirectoryInfo dir)
                {
                    IsCut = _ignoreList.IsIgnored(dir) == true;
                }
                else if (Info is FileInfo file)
                {
                    IsCut = _ignoreList.IsIgnored(file) == true;
                }
            }
        }

        private bool ShouldShowEntry(string path)
        {
            PatternMatchingResult result = _globbingMatcher.Match(path);
            return !result.HasMatches;
        }

        private void RefreshChildren()
        {
            _children ??= [];

            // Refresh the cached state of the FileSystemInfo before checking Exists
            // This is important because FileSystemInfo caches its state and rapid file changes
            // (like during Copilot edits) can cause stale cached values
            Info.Refresh();

            if (!Info.Exists)
            {
                return;
            }

            var existingNodes = _children.ToDictionary(n => n.Info.FullName, n => n);
            var nodesToDispose = existingNodes.Keys.ToHashSet();
            List<WorkspaceItemNode> activeNodes = [];

            // PERFORMANCE OPTIMIZATION: Use EnumerateFileSystemInfos instead of GetFileSystemEntries
            // This provides lazy enumeration and avoids creating arrays for large directories
            try
            {
                IEnumerable<FileSystemInfo> entries = ((DirectoryInfo)Info).EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly);

                foreach (FileSystemInfo entry in entries)
                {
                    // Early filtering before path allocation to reduce memory pressure
                    var name = entry.Name;
                    if (IsSystemFile(name))
                    {
                        continue;
                    }

                    var fullPath = entry.FullName;
                    if (!ShouldShowEntry(fullPath))
                    {
                        continue;
                    }

                    if (existingNodes.TryGetValue(fullPath, out WorkspaceItemNode existingNode))
                    {
                        _ = nodesToDispose.Remove(fullPath);
                        activeNodes.Add(existingNode);
                    }
                    else
                    {
                        // Avoid redundant File.Exists/Directory.Exists calls since we already have FileSystemInfo
                        activeNodes.Add(new WorkspaceItemNode(this, entry, _ignoreList, _globbingMatcher));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Handle permission issues gracefully - directory exists but can't be read
                return;
            }
            catch (DirectoryNotFoundException)
            {
                // Directory was deleted between checks - don't clear children as this may be temporary
                return;
            }
            catch (IOException)
            {
                // IO error (e.g., directory being modified) - don't clear children as this may be temporary
                return;
            }

            // Early exit optimization: If there are no nodes to dispose and all active nodes are already in the children collection, we don't need to do anything
            if (nodesToDispose.Count == 0 && activeNodes.Count == _children.Count)
            {
                return;
            }

            foreach (var node in nodesToDispose)
            {
                existingNodes[node].Dispose();
            }

            // PERFORMANCE OPTIMIZATION: Use more efficient sorting with pre-computed type information
            activeNodes.Sort(_compareNodes);

            // UI thread is required for collection updates to properly refresh the tree view
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _children.BeginBulkOperation();
                _children.Clear();
                _children.AddRange(activeNodes);
                _children.EndBulkOperation();
                HasItems = _children.Count > 0;

                RaisePropertyChanged(nameof(HasItems));
                RaisePropertyChanged(nameof(Items));
            });
        }

        // PERFORMANCE OPTIMIZATION: Pre-compiled delegate for faster sorting
        private static readonly Comparison<WorkspaceItemNode> _compareNodes = (lhs, rhs) =>
        {
            var lhsIsDir = lhs.Info is DirectoryInfo;
            var rhsIsDir = rhs.Info is DirectoryInfo;

            return lhsIsDir != rhsIsDir ? lhsIsDir ? -1 : 1 : StringComparer.OrdinalIgnoreCase.Compare(lhs.Text, rhs.Text);
        };

        // PERFORMANCE OPTIMIZATION: Fast system file detection using spans for minimal allocations
        private static bool IsSystemFile(string fileName)
        {
            // Check common system files/patterns quickly without allocations
            if (fileName.Length == 0)
            {
                return true;
            }

            ReadOnlySpan<char> span = fileName.AsSpan();

            // Hidden files starting with '.':
            if (span[0] == '.' && span.Length > 1)
            {
                return span switch
                {
                    ".git" => true,
                    _ => false
                };
            }

            // Temporary files
            if (span[0] == '~' || span.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Common build output directories
            return span switch
            {
                "bin" or "obj" or "node_modules" or "packages" => true,
                _ => false
            };
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // PERFORMANCE OPTIMIZATION: Enhanced filtering with span-based path checking to reduce allocations
            ReadOnlySpan<char> pathSpan = e.FullPath.AsSpan();

            // Fast rejection of system paths using ReadOnlySpan to avoid string allocations
            if (ContainsSystemPath(pathSpan))
            {
                return;
            }

            RefreshAsync().FireAndForget();
        }

        // PERFORMANCE OPTIMIZATION: Use spans for faster path checking without string allocations
        // Checks if the path contains a directory segment matching common system directories
        private static bool ContainsSystemPath(ReadOnlySpan<char> path)
        {
            // Check for common system directories efficiently by looking for directory segments
            // We need to match "\name\" or "\name" at end to avoid false positives like "combine.txt" matching "bin"
            return ContainsPathSegment(path, ".vs") ||
                   ContainsPathSegment(path, "node_modules") ||
                   ContainsPathSegment(path, ".git") ||
                   ContainsPathSegment(path, ".tmp") ||
                   path.Contains("~", StringComparison.OrdinalIgnoreCase);
        }

        // Checks if a path contains a specific directory segment (bounded by path separators)
        private static bool ContainsPathSegment(ReadOnlySpan<char> path, ReadOnlySpan<char> segment)
        {
            var index = path.IndexOf(segment, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                // Check if it's a proper path segment (preceded by separator or start, followed by separator or end)
                var startOk = index == 0 || path[index - 1] == '\\' || path[index - 1] == '/';
                var endIndex = index + segment.Length;
                var endOk = endIndex == path.Length || path[endIndex] == '\\' || path[endIndex] == '/';

                if (startOk && endOk)
                {
                    return true;
                }

                // Continue searching from the next position
                if (endIndex >= path.Length)
                {
                    break;
                }

                var nextIndex = path.Slice(endIndex).IndexOf(segment, StringComparison.OrdinalIgnoreCase);
                index = nextIndex >= 0 ? endIndex + nextIndex : -1;
            }

            return false;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;

            if (_watcher != null)
            {
                _watcher.Created -= OnFileSystemChanged;
                _watcher.Deleted -= OnFileSystemChanged;
                _watcher.Renamed -= OnFileSystemChanged;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
            }

            if (_children != null)
            {
                foreach (WorkspaceItemNode item in _children)
                {
                    item.Dispose();
                }
            }
        }

        public int CompareTo(object obj)
        {
            return obj is ITreeDisplayItem item ? StringComparer.OrdinalIgnoreCase.Compare(Text, item.Text) : 0;
        }

        public object GetBrowseObject() => this;

        public TPattern GetPattern<TPattern>() where TPattern : class
        {
            if (!IsDisposed)
            {
                if (_supportedPatterns.Contains(typeof(TPattern)))
                {
                    return this as TPattern;
                }
            }
            else
            {
                // If this item has been deleted, it no longer supports any patterns
                // other than ISupportDisposalNotification.
                // It's valid to use GetPattern on a deleted item, but there are no
                // longer any pattern contracts it fulfills other than the contract
                // that reports the item as a dead ITransientObject.
                if (typeof(TPattern) == typeof(ISupportDisposalNotification))
                {
                    return this as TPattern;
                }
            }

            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public async Task RefreshAsync()
        {
            // Only refresh directories that have already been expanded (i.e., _children is not null).
            // For unexpanded nodes, the initial HasItems value from the constructor is sufficient.
            // Refreshing unexpanded nodes would trigger RefreshChildren() which applies filtering
            // that may differ from the constructor's unfiltered HasItems check, causing expanders
            // to incorrectly disappear.
            if (Info is not FileInfo && Info.Exists && _children != null)
            {
                await TaskScheduler.Default;
                Debouncer.Debounce(Info.FullName, RefreshChildren, 250);
            }
        }

        public void CancelLoad()
        {

        }

        // IDragDropSourcePattern

        public DirectionalDropArea SupportedAreas => DirectionalDropArea.On;

        public void OnDragEnter(DirectionalDropArea dropArea, DragEventArgs e)
        {
            if (Info is DirectoryInfo && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        public void OnDragOver(DirectionalDropArea dropArea, DragEventArgs e)
        {
            if (Info is DirectoryInfo && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        public void OnDragLeave(DirectionalDropArea dropArea, DragEventArgs e)
        {
            if (Info is DirectoryInfo && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        public void OnDrop(DirectionalDropArea dropArea, DragEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
                {
                    return;
                }

                DTE dte = VS.GetRequiredService<DTE, DTE>();

                foreach (var path in paths)
                {
                    if (dte.Solution.FindProjectItem(path) != null)
                    {
                        _ = VS.MessageBox.ShowError("You cannot move a file that is part of a project");
                        return;
                    }

                    if (File.Exists(path))
                    {
                        File.Move(path, Path.Combine(Info.FullName, Path.GetFileName(path)));
                    }
                    else if (Directory.Exists(path))
                    {
                        var dir = new DirectoryInfo(path);
                        dir.MoveTo(Path.Combine(Info.FullName, dir.Name));
                    }
                }

                e.Handled = true;
            }
        }
    }
}