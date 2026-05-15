using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceFiles.Services;

namespace WorkspaceFiles.Test
{
    [TestClass]
    public class WorkspaceSettingsServiceTests
    {
        private string _tempDir;

        [TestInitialize]
        public void Initialize()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "WorkspaceFilesTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        #region GetSettingsFilePath

        [TestMethod]
        public void WhenSolutionIsSlnThenSettingsFilePathIsCorrect()
        {
            var solutionPath = Path.Combine(@"C:\Projects\MySolution", "MySolution.sln");
            var expected = Path.Combine(@"C:\Projects\MySolution", "MySolution.wsfiles.json");

            var result = WorkspaceSettingsService.GetSettingsFilePath(solutionPath);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void WhenSolutionIsSlnxThenSettingsFilePathIsCorrect()
        {
            var solutionPath = Path.Combine(@"C:\Projects\MySolution", "MySolution.slnx");
            var expected = Path.Combine(@"C:\Projects\MySolution", "MySolution.wsfiles.json");

            var result = WorkspaceSettingsService.GetSettingsFilePath(solutionPath);

            Assert.AreEqual(expected, result);
        }

        #endregion

        #region LoadFolders

        [TestMethod]
        public void WhenSettingsFileDoesNotExistThenLoadFoldersReturnsNull()
        {
            var path = Path.Combine(_tempDir, "nonexistent.wsfiles.json");

            var result = WorkspaceSettingsService.LoadFolders(path);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void WhenSettingsFileHasFoldersThenLoadFoldersReturnsCorrectList()
        {
            var path = Path.Combine(_tempDir, "test.wsfiles.json");
            File.WriteAllText(path, "{\"folders\":[\"relative/path1\",\"../other\"]}");

            var result = WorkspaceSettingsService.LoadFolders(path);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("relative/path1", result[0]);
            Assert.AreEqual("../other", result[1]);
        }

        [TestMethod]
        public void WhenSettingsFileHasEmptyFoldersThenLoadFoldersReturnsEmptyList()
        {
            var path = Path.Combine(_tempDir, "empty.wsfiles.json");
            File.WriteAllText(path, "{\"folders\":[]}");

            var result = WorkspaceSettingsService.LoadFolders(path);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        #endregion

        #region SaveFolders

        [TestMethod]
        public void WhenFoldersAreSavedThenFileIsCreatedWithCorrectContent()
        {
            var path = Path.Combine(_tempDir, "save.wsfiles.json");

            WorkspaceSettingsService.SaveFolders(path, new[] { "folder1", "folder2" });

            Assert.IsTrue(File.Exists(path));
            var reloaded = WorkspaceSettingsService.LoadFolders(path);
            Assert.IsNotNull(reloaded);
            Assert.AreEqual(2, reloaded.Count);
            Assert.AreEqual("folder1", reloaded[0]);
            Assert.AreEqual("folder2", reloaded[1]);
        }

        [TestMethod]
        public void WhenFoldersAreSavedTwiceThenFileIsOverwritten()
        {
            var path = Path.Combine(_tempDir, "overwrite.wsfiles.json");
            WorkspaceSettingsService.SaveFolders(path, new[] { "old" });

            WorkspaceSettingsService.SaveFolders(path, new[] { "new1", "new2" });

            var result = WorkspaceSettingsService.LoadFolders(path);
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("new1", result[0]);
            Assert.AreEqual("new2", result[1]);
        }

        [TestMethod]
        public void WhenEmptyFoldersAreSavedThenExistingFileIsDeleted()
        {
            var path = Path.Combine(_tempDir, "delete.wsfiles.json");
            WorkspaceSettingsService.SaveFolders(path, new[] { "something" });
            Assert.IsTrue(File.Exists(path));

            WorkspaceSettingsService.SaveFolders(path, Array.Empty<string>());

            Assert.IsFalse(File.Exists(path));
        }

        [TestMethod]
        public void WhenEmptyFoldersAreSavedAndFileDoesNotExistThenNoExceptionIsThrown()
        {
            var path = Path.Combine(_tempDir, "nonexistent.wsfiles.json");

            WorkspaceSettingsService.SaveFolders(path, Array.Empty<string>());

            Assert.IsFalse(File.Exists(path));
        }

        #endregion

        #region MigrateFromGlobals

        [TestMethod]
        public void WhenGlobalsValueHasMultipleFoldersThenMigrateReturnsParsedList()
        {
            var result = WorkspaceSettingsService.MigrateFromGlobals("folder1|folder2|../other");

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("folder1", result[0]);
            Assert.AreEqual("folder2", result[1]);
            Assert.AreEqual("../other", result[2]);
        }

        [TestMethod]
        public void WhenGlobalsValueIsNullThenMigrateReturnsEmptyList()
        {
            var result = WorkspaceSettingsService.MigrateFromGlobals(null);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void WhenGlobalsValueIsEmptyThenMigrateReturnsEmptyList()
        {
            var result = WorkspaceSettingsService.MigrateFromGlobals(string.Empty);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void WhenGlobalsValueHasOneFolderThenMigrateReturnsSingleEntry()
        {
            var result = WorkspaceSettingsService.MigrateFromGlobals("src");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("src", result[0]);
        }

        #endregion
    }
}
