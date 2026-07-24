using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspaceFiles.Test
{
    [TestClass]
    public class RenameCommandTests
    {
        private string _testDir;

        [TestInitialize]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "WorkspaceFilesTests", "RenameCommand", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void TearDown()
        {
            Directory.Delete(_testDir, recursive: true);
        }

        // --- file cases ---

        [TestMethod]
        [Description("Renaming a file to an existing name should be detected as a conflict.")]
        public void WhenTargetFileAlreadyExistsThenTargetAlreadyExistsReturnsTrue()
        {
            var sourceFile = Path.Combine(_testDir, "source.txt");
            var existingFile = Path.Combine(_testDir, "existing.txt");
            File.WriteAllText(sourceFile, "source");
            File.WriteAllText(existingFile, "existing");

            // Bug: uses Path.Combine(oldItemPath, newName) instead of
            //      Path.Combine(Path.GetDirectoryName(oldItemPath), newName),
            // so File.Exists always returns false and the conflict is missed.
            var result = RenameCommand.TargetAlreadyExists(sourceFile, "existing.txt", isDirectory: false);

            Assert.IsTrue(result, "Expected conflict to be detected when target file already exists.");
        }

        [TestMethod]
        [Description("Renaming a file to a free name should not be detected as a conflict.")]
        public void WhenTargetFileDoesNotExistThenTargetAlreadyExistsReturnsFalse()
        {
            var sourceFile = Path.Combine(_testDir, "source.txt");
            File.WriteAllText(sourceFile, "source");

            var result = RenameCommand.TargetAlreadyExists(sourceFile, "free-name.txt", isDirectory: false);

            Assert.IsFalse(result, "Expected no conflict when target file does not exist.");
        }

        // --- directory cases (correct code, should pass today) ---

        [TestMethod]
        [Description("Renaming a directory to an existing name should be detected as a conflict.")]
        public void WhenTargetDirectoryAlreadyExistsThenTargetAlreadyExistsReturnsTrue()
        {
            var sourceDir = Path.Combine(_testDir, "source");
            var existingDir = Path.Combine(_testDir, "existing");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(existingDir);

            var result = RenameCommand.TargetAlreadyExists(sourceDir, "existing", isDirectory: true);

            Assert.IsTrue(result, "Expected conflict to be detected when target directory already exists.");
        }

        [TestMethod]
        [Description("Renaming a directory to a free name should not be detected as a conflict.")]
        public void WhenTargetDirectoryDoesNotExistThenTargetAlreadyExistsReturnsFalse()
        {
            var sourceDir = Path.Combine(_testDir, "source");
            Directory.CreateDirectory(sourceDir);

            var result = RenameCommand.TargetAlreadyExists(sourceDir, "free-name", isDirectory: true);

            Assert.IsFalse(result, "Expected no conflict when target directory does not exist.");
        }
    }
}
