using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

namespace WorkspaceFiles.Services
{
    /// <summary>
    /// Represents the Git status of a file or folder.
    /// </summary>
    internal enum GitFileStatus
    {
        /// <summary>File is not in a Git repository.</summary>
        NotInRepo,
        /// <summary>File is untracked (not added to Git).</summary>
        Untracked,
        /// <summary>File is ignored by Git.</summary>
        Ignored,
        /// <summary>File is committed and unmodified.</summary>
        Unmodified,
        /// <summary>File has been modified locally.</summary>
        Modified,
        /// <summary>File has been staged for commit.</summary>
        Staged,
        /// <summary>File is newly added and staged.</summary>
        Added,
        /// <summary>File has been deleted.</summary>
        Deleted,
        /// <summary>File has been renamed.</summary>
        Renamed,
        /// <summary>File has merge conflicts.</summary>
        Conflict
    }

    /// <summary>
    /// Service for getting Git status of files with efficient caching.
    /// Designed for performance in Solution Explorer scenarios with many file nodes.
    /// </summary>
    internal static class GitStatusService
    {
        private static readonly ConcurrentDictionary<string, CachedStatus> _statusCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(5);
        private static readonly object _refreshLock = new();
        private static DateTime _lastRefresh = DateTime.MinValue;

        private sealed class CachedStatus
        {
            public GitFileStatus Status { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Gets the cached Git status for a file synchronously.
        /// Returns the cached value if available, or NotInRepo if not yet loaded.
        /// Use <see cref="GetFileStatusAsync"/> to ensure fresh data.
        /// </summary>
        public static GitFileStatus GetCachedFileStatus(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return GitFileStatus.NotInRepo;
            }

            // Normalize the file path for consistent cache lookups
            try
            {
                filePath = Path.GetFullPath(filePath);
            }
            catch
            {
                return GitFileStatus.NotInRepo;
            }

            if (_statusCache.TryGetValue(filePath, out CachedStatus cached))
            {
                return cached.Status;
            }

            // Check if any parent directory has a status (e.g., git reports "?? folder/" for untracked folders)
            GitFileStatus parentStatus = GetParentDirectoryStatus(filePath);
            if (parentStatus != GitFileStatus.NotInRepo)
            {
                return parentStatus;
            }

            return GitFileStatus.NotInRepo;
        }

        /// <summary>
        /// Gets the Git status for a file asynchronously.
        /// Refreshes the cache if stale and returns the current status.
        /// </summary>
        public static async Task<GitFileStatus> GetFileStatusAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return GitFileStatus.NotInRepo;
            }

            // Normalize the file path for consistent cache lookups
            filePath = Path.GetFullPath(filePath);

            // Check cache first with expiration
            if (_statusCache.TryGetValue(filePath, out CachedStatus cached) &&
                DateTime.UtcNow - cached.Timestamp < _cacheExpiration)
            {
                return cached.Status;
            }

            // Run Git operations on a background thread to avoid blocking UI
            return await Task.Run(() => GetFileStatusCore(filePath));
        }

        private static GitFileStatus GetFileStatusCore(string filePath)
        {
            // Normalize the file path for consistent cache lookups
            filePath = Path.GetFullPath(filePath);

            // Find repo root
            var repoRoot = FindGitRoot(filePath);
            if (string.IsNullOrEmpty(repoRoot))
            {
                return GitFileStatus.NotInRepo;
            }

            // Refresh status for all files if cache is stale
            if (DateTime.UtcNow - _lastRefresh > _cacheExpiration)
            {
                lock (_refreshLock)
                {
                    // Double-check inside lock
                    if (DateTime.UtcNow - _lastRefresh > _cacheExpiration)
                    {
                        RefreshStatusCache(repoRoot);
                        _lastRefresh = DateTime.UtcNow;
                    }
                }
            }

            // Return cached status
            if (_statusCache.TryGetValue(filePath, out CachedStatus cached))
            {
                return cached.Status;
            }

            // Check parent directory status for files in untracked folders
            GitFileStatus parentStatus = GetParentDirectoryStatus(filePath);
            if (parentStatus != GitFileStatus.NotInRepo)
            {
                return parentStatus;
            }

            // If not in cache after refresh, it's likely unmodified (committed and clean)
            return GitFileStatus.Unmodified;
        }

        /// <summary>
        /// Gets the appropriate state icon for a Git file status.
        /// </summary>
        public static ImageMoniker GetStatusIcon(GitFileStatus status)
        {
            return status switch
            {
                GitFileStatus.Unmodified => KnownMonikers.CheckedInNode,
                GitFileStatus.Modified => KnownMonikers.CheckedOutForEditNode,
                GitFileStatus.Staged => KnownMonikers.Checkmark,
                GitFileStatus.Added or GitFileStatus.Untracked => KnownMonikers.PendingAddNode,
                GitFileStatus.Deleted => KnownMonikers.PendingDeleteNode,
                GitFileStatus.Conflict => KnownMonikers.StatusWarning,
                GitFileStatus.Ignored => KnownMonikers.HideMember,
                GitFileStatus.Renamed => KnownMonikers.PendingRenameNode,
                // NotInRepo and unknown states show no icon
                _ => default,
            };
        }

        /// <summary>
        /// Gets a human-readable tooltip text for a Git file status.
        /// </summary>
        public static string GetStatusTooltip(GitFileStatus status)
        {
            return status switch
            {
                GitFileStatus.Unmodified => "Unchanged",
                GitFileStatus.Modified => "Pending - Edit",
                GitFileStatus.Staged => "Staged",
                GitFileStatus.Added => "Pending - Add",
                GitFileStatus.Untracked => "Untracked",
                GitFileStatus.Deleted => "Pending - Delete",
                GitFileStatus.Conflict => "Merge Conflict",
                GitFileStatus.Ignored => "Ignored",
                GitFileStatus.Renamed => "Pending - Rename",
                _ => string.Empty,
            };
        }

        /// <summary>
        /// Invalidates the cache, forcing a refresh on next status request.
        /// Call this when you know files have changed (e.g., after a git operation).
        /// </summary>
        public static void InvalidateCache()
        {
            _statusCache.Clear();
            _lastRefresh = DateTime.MinValue;
        }

        /// <summary>
        /// Marks the cache as stale, forcing a re-fetch of git status on next request.
        /// The refresh will clear repo-specific cache entries before fetching new status,
        /// ensuring files that become clean are properly updated.
        /// </summary>
        public static void MarkCacheStale()
        {
            _lastRefresh = DateTime.MinValue;
        }

        private static GitFileStatus GetParentDirectoryStatus(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);

            while (!string.IsNullOrEmpty(directory))
            {
                // Check both with and without trailing slash since git may report "?? folder/"
                if (_statusCache.TryGetValue(directory, out CachedStatus cached) ||
                    _statusCache.TryGetValue(directory + Path.DirectorySeparatorChar, out cached))
                {
                    return cached.Status;
                }

                var parent = Path.GetDirectoryName(directory);
                if (parent == directory)
                {
                    break; // Reached root
                }

                directory = parent;
            }

            return GitFileStatus.NotInRepo;
        }

        private static string FindGitRoot(string path)
        {
            var current = Directory.Exists(path) ? path : Path.GetDirectoryName(path);

            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, ".git")))
                {
                    return current;
                }

                current = Path.GetDirectoryName(current);
            }

            return null;
        }

        private static void RefreshStatusCache(string repoRoot)
        {
            try
            {
                // Clear existing cache entries for this repo before refreshing.
                // This is essential so that files that become "clean" (no longer in git status)
                // are properly removed from cache and will return Unmodified status.
                var repoRootNormalized = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var keysToRemove = new List<string>();
                foreach (var key in _statusCache.Keys)
                {
                    if (key.StartsWith(repoRootNormalized, StringComparison.OrdinalIgnoreCase))
                    {
                        keysToRemove.Add(key);
                    }
                }
                foreach (var key in keysToRemove)
                {
                    _statusCache.TryRemove(key, out _);
                }

                // Get status for all files using porcelain format for easy parsing
                // --porcelain=v1 gives us: XY filename
                // X = index status, Y = working tree status
                var output = RunGitCommand(repoRoot, "status --porcelain=v1");
                if (string.IsNullOrEmpty(output))
                {
                    // No output means all files are clean - cache is already cleared above
                    return;
                }

                // Parse each line of output
                using (var reader = new StringReader(output))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length < 3)
                        {
                            continue;
                        }

                        var indexStatus = line[0];
                        var workTreeStatus = line[1];
                        var relativePath = line.Substring(3).Trim().Trim('"');

                        // Handle renamed files: "R  old -> new"
                        if (relativePath.Contains(" -> "))
                        {
                            var parts = relativePath.Split([" -> "], StringSplitOptions.None);
                            relativePath = parts.Length > 1 ? parts[1] : parts[0];
                        }

                        // Normalize path separators (git uses forward slashes)
                        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

                        try
                        {
                            var fullPath = Path.GetFullPath(Path.Combine(repoRoot, relativePath));
                            GitFileStatus status = ParseGitStatus(indexStatus, workTreeStatus);

                            _statusCache[fullPath] = new CachedStatus
                            {
                                Status = status,
                                Timestamp = DateTime.UtcNow
                            };
                        }
                        catch
                        {
                            // Skip paths that can't be resolved
                        }
                    }
                }
            }
            catch
            {
                // Silently fail - Git status is a nice-to-have feature
            }
        }

        private static GitFileStatus ParseGitStatus(char indexStatus, char workTreeStatus)
        {
            // Check for conflicts first (both modified or unmerged states)
            if (indexStatus == 'U' || workTreeStatus == 'U' ||
                (indexStatus == 'A' && workTreeStatus == 'A') ||
                (indexStatus == 'D' && workTreeStatus == 'D'))
            {
                return GitFileStatus.Conflict;
            }

            // Check working tree status first (local changes take priority)
            return workTreeStatus switch
            {
                'M' => GitFileStatus.Modified,
                'D' => GitFileStatus.Deleted,
                '?' => GitFileStatus.Untracked,
                '!' => GitFileStatus.Ignored,
                // Check index status (staged changes)
                _ => indexStatus switch
                {
                    'M' => GitFileStatus.Staged,
                    'A' => GitFileStatus.Added,
                    'D' => GitFileStatus.Deleted,
                    'R' => GitFileStatus.Renamed,
                    _ => GitFileStatus.Unmodified,
                },
            };
        }

        private static string RunGitCommand(string workingDirectory, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var exited = process.WaitForExit(5000);

                    if (!exited)
                    {
                        try { process.Kill(); } catch { }
                        return null;
                    }

                    return process.ExitCode == 0 ? output : null;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
