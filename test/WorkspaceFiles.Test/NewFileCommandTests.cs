using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspaceFiles.Test
{
    [TestClass]
    public class NewFileCommandTests
    {
        [TestCleanup]
        public void TearDown()
        {
            // WorkspaceItemContextMenuController.CurrentItems is process-global static state;
            // reset it so tests don't leak selection into one another.
            WorkspaceItemContextMenuController.SetCurrentItems(null);
        }

        // --- ResolveFolderFor: pure per-node-type rule ---

        [TestMethod]
        [Description("A Root node's own path is used directly as the target folder.")]
        public void WhenTypeIsRootThenResolveFolderForReturnsSamePath()
        {
            var path = @"C:\repo";

            var result = NewFileCommand.ResolveFolderFor(WorkspaceItemType.Root, path);

            Assert.AreEqual(path, result);
        }

        [TestMethod]
        [Description("A Folder node's own path is used directly as the target folder.")]
        public void WhenTypeIsFolderThenResolveFolderForReturnsSamePath()
        {
            var path = @"C:\repo\src";

            var result = NewFileCommand.ResolveFolderFor(WorkspaceItemType.Folder, path);

            Assert.AreEqual(path, result);
        }

        [TestMethod]
        [Description("A File node resolves to its parent directory (sibling creation), matching native VS Add New Item semantics.")]
        public void WhenTypeIsFileThenResolveFolderForReturnsParentDirectory()
        {
            var path = @"C:\repo\src\Program.cs";

            var result = NewFileCommand.ResolveFolderFor(WorkspaceItemType.File, path);

            Assert.AreEqual(@"C:\repo\src", result);
        }

        // --- ResolveTargetFolder: reads the live selection ---

        [TestMethod]
        [Description("When nothing is selected, there is no target folder (so the command should hide/fall through).")]
        public void WhenNoItemsAreSelectedThenResolveTargetFolderReturnsNull()
        {
            WorkspaceItemContextMenuController.SetCurrentItems(new WorkspaceItemNode[0]);

            var result = NewFileCommand.ResolveTargetFolder();

            Assert.IsNull(result);
        }

        [TestMethod]
        [Description("A single selected Folder node resolves to its own path.")]
        public void WhenSingleFolderIsSelectedThenResolveTargetFolderReturnsItsPath()
        {
            var folderNode = CreateNode(@"C:\repo\src", isFile: false);
            WorkspaceItemContextMenuController.SetCurrentItems(new[] { folderNode });

            var result = NewFileCommand.ResolveTargetFolder();

            Assert.AreEqual(@"C:\repo\src", result);
        }

        [TestMethod]
        [Description("A single selected File node resolves to its parent directory.")]
        public void WhenSingleFileIsSelectedThenResolveTargetFolderReturnsParentDirectory()
        {
            var fileNode = CreateNode(@"C:\repo\src\Program.cs", isFile: true);
            WorkspaceItemContextMenuController.SetCurrentItems(new[] { fileNode });

            var result = NewFileCommand.ResolveTargetFolder();

            Assert.AreEqual(@"C:\repo\src", result);
        }

        [TestMethod]
        [Description("With multiple items selected, the first selected item's folder is used.")]
        public void WhenMultipleItemsAreSelectedThenResolveTargetFolderUsesFirstItem()
        {
            var firstNode = CreateNode(@"C:\repo\src\Program.cs", isFile: true);
            var secondNode = CreateNode(@"C:\repo\docs", isFile: false);
            WorkspaceItemContextMenuController.SetCurrentItems(new[] { firstNode, secondNode });

            var result = NewFileCommand.ResolveTargetFolder();

            Assert.AreEqual(@"C:\repo\src", result);
        }

        private static WorkspaceItemNode CreateNode(string path, bool isFile)
        {
            FileSystemInfo info = isFile ? (FileSystemInfo)new FileInfo(path) : new DirectoryInfo(path);

            // parent: null (anything other than a WorkspaceRootNode yields Folder/File based on info type)
            // ignoreList/globbingMatcher: null, createWatcher: false — avoids touching the file system
            // watcher or git status services, which aren't available/needed for this pure logic test.
            return new WorkspaceItemNode(parent: null, info, ignoreList: null, globbingMatcher: null, createWatcher: false);
        }
    }
}
