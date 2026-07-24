using System.IO;

namespace WorkspaceFiles.Services
{
    /// <summary>
    /// Detects Git repository roots for regular repositories and linked worktrees.
    /// In linked worktrees, the <c>.git</c> entry is a file that points to the
    /// worktree metadata location instead of a directory.
    /// See: https://git-scm.com/docs/git-worktree#_details
    /// </summary>
    internal static class GitRepositoryDetector
    {
        /// <summary>
        /// Traverses parent directories until it finds a Git marker.
        /// </summary>
        public static bool TryFindGitRoot(string path, out string repoRoot)
        {
            repoRoot = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var current = Directory.Exists(path) ? path : Path.GetDirectoryName(path);

            while (!string.IsNullOrEmpty(current))
            {
                if (HasGitMarker(current))
                {
                    repoRoot = current;
                    return true;
                }

                current = Path.GetDirectoryName(current);
            }

            return false;
        }

        /// <summary>
        /// Determines whether a directory contains a Git marker.
        /// The marker can be either a <c>.git</c> directory (main worktree)
        /// or a <c>.git</c> file (linked worktree).
        /// See: https://git-scm.com/docs/git-worktree#_details
        /// </summary>
        public static bool HasGitMarker(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            var dotGitPath = Path.Combine(directoryPath, ".git");
            return Directory.Exists(dotGitPath) || File.Exists(dotGitPath);
        }
    }
}
