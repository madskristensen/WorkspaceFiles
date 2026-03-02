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
    }
}
