using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceFiles.Services;

namespace WorkspaceFiles.Test
{
    [TestClass]
    public class GitStatusServiceTests
    {
        [DataTestMethod]
        [DataRow((int)GitFileStatus.Unmodified, "Unchanged")]
        [DataRow((int)GitFileStatus.Modified, "Pending - Edit")]
        [DataRow((int)GitFileStatus.Staged, "Staged")]
        [DataRow((int)GitFileStatus.Added, "Pending - Add")]
        [DataRow((int)GitFileStatus.Untracked, "Untracked")]
        [DataRow((int)GitFileStatus.Deleted, "Pending - Delete")]
        [DataRow((int)GitFileStatus.Conflict, "Merge Conflict")]
        [DataRow((int)GitFileStatus.Ignored, "Ignored")]
        [DataRow((int)GitFileStatus.Renamed, "Pending - Rename")]
        [DataRow((int)GitFileStatus.NotInRepo, "")]
        public void WhenStatusTooltipIsRequestedThenExpectedTextIsReturned(int statusValue, string expectedTooltip)
        {
            var status = (GitFileStatus)statusValue;
            var tooltip = GitStatusService.GetStatusTooltip(status);

            Assert.AreEqual(expectedTooltip, tooltip);
        }

        [TestMethod]
        public void WhenCachedStatusIsRequestedWithNullPathThenNotInRepoIsReturned()
        {
            var status = GitStatusService.GetCachedFileStatus(null);

            Assert.AreEqual(GitFileStatus.NotInRepo, status);
        }

        [TestMethod]
        public void WhenCachedStatusIsRequestedWithEmptyPathThenNotInRepoIsReturned()
        {
            var status = GitStatusService.GetCachedFileStatus(string.Empty);

            Assert.AreEqual(GitFileStatus.NotInRepo, status);
        }

        [TestMethod]
        public void WhenCachedStatusIsRequestedWithInvalidPathThenNotInRepoIsReturned()
        {
            var status = GitStatusService.GetCachedFileStatus("\0");

            Assert.AreEqual(GitFileStatus.NotInRepo, status);
        }

        [DataTestMethod]
        [DataRow(" M src/file.cs", (int)GitFileStatus.Modified, "src/file.cs")]
        [DataRow("M  src/file.cs", (int)GitFileStatus.Staged, "src/file.cs")]
        [DataRow("A  src/file.cs", (int)GitFileStatus.Added, "src/file.cs")]
        [DataRow("R  old/name.cs -> new/name.cs", (int)GitFileStatus.Renamed, "new/name.cs")]
        [DataRow("?? src/newfile.cs", (int)GitFileStatus.Untracked, "src/newfile.cs")]
        [DataRow("!! src/ignored.cs", (int)GitFileStatus.Ignored, "src/ignored.cs")]
        [DataRow("UU src/conflict.cs", (int)GitFileStatus.Conflict, "src/conflict.cs")]
        [DataRow("AA src/conflict.cs", (int)GitFileStatus.Conflict, "src/conflict.cs")]
        [DataRow("DD src/conflict.cs", (int)GitFileStatus.Conflict, "src/conflict.cs")]
        [DataRow(" M \"folder/file name.cs\"", (int)GitFileStatus.Modified, "folder/file name.cs")]
        [DataRow("MD src/deleted.cs", (int)GitFileStatus.Deleted, "src/deleted.cs")]
        [DataRow("DM src/modified.cs", (int)GitFileStatus.Modified, "src/modified.cs")]
        public void WhenPorcelainLineIsValidThenExpectedStatusAndPathAreParsed(string line, int expectedStatusValue, string expectedPathSuffix)
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), "WorkspaceFilesTests", "Repo");

            var parsed = GitStatusService.TryParsePorcelainStatusLine(line, repoRoot, out var fullPath, out var status);

            Assert.IsTrue(parsed);
            Assert.AreEqual((GitFileStatus)expectedStatusValue, status);
            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(repoRoot, expectedPathSuffix.Replace('/', Path.DirectorySeparatorChar))),
                fullPath);
        }

        [TestMethod]
        public void WhenPorcelainLineIsInvalidThenParseReturnsFalse()
        {
            var parsed = GitStatusService.TryParsePorcelainStatusLine("", "C:\\repo", out var fullPath, out var status);

            Assert.IsFalse(parsed);
            Assert.IsNull(fullPath);
            Assert.AreEqual(GitFileStatus.NotInRepo, status);
        }
    }
}
