using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceFiles.Services;

namespace WorkspaceFiles.Test
{
    [TestClass]
    public class GitRepositoryDetectorTests
    {
        [TestMethod]
        public void WhenDirectoryContainsGitFolderThenHasGitMarkerReturnsTrue()
        {
            var path = CreateTempDirectory();
            Directory.CreateDirectory(Path.Combine(path, ".git"));

            var hasGitMarker = GitRepositoryDetector.HasGitMarker(path);

            Assert.IsTrue(hasGitMarker);
        }

        [TestMethod]
        public void WhenDirectoryContainsGitFileThenHasGitMarkerReturnsTrue()
        {
            var path = CreateTempDirectory();
            File.WriteAllText(Path.Combine(path, ".git"), "gitdir: D:/repo/.git/worktrees/dev");

            var hasGitMarker = GitRepositoryDetector.HasGitMarker(path);

            Assert.IsTrue(hasGitMarker);
        }

        [TestMethod]
        public void WhenDirectoryDoesNotContainGitMarkerThenHasGitMarkerReturnsFalse()
        {
            var path = CreateTempDirectory();

            var hasGitMarker = GitRepositoryDetector.HasGitMarker(path);

            Assert.IsFalse(hasGitMarker);
        }

        [TestMethod]
        public void WhenDirectoryPathIsNullThenHasGitMarkerReturnsFalse()
        {
            var hasGitMarker = GitRepositoryDetector.HasGitMarker(null);

            Assert.IsFalse(hasGitMarker);
        }

        [TestMethod]
        public void WhenDirectoryPathIsWhitespaceThenHasGitMarkerReturnsFalse()
        {
            var hasGitMarker = GitRepositoryDetector.HasGitMarker(" ");

            Assert.IsFalse(hasGitMarker);
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "WorkspaceFilesTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
