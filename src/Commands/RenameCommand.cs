using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    [Command(PackageIds.Rename)]
    internal sealed class RenameCommand : BaseCommand<RenameCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Enabled = WorkspaceItemContextMenuController.CurrentItems.Count == 1;
        }

        protected override void Execute(object sender, EventArgs e)
        {
            var oldItemPath = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;
            var oldItemName = WorkspaceItemContextMenuController.CurrentItem.Info.Name;

            var fileAttributes = File.GetAttributes(oldItemPath);
            var isDirectory = fileAttributes.HasFlag(FileAttributes.Directory);

            var result = TextInputDialog.Show(
                "Rename File",
                $"Enter the new name of the file for {oldItemName}.",
                oldItemName,
                userInput =>
                {
                    var OperationResult = string.Empty;

                    if (!userInput.Equals(oldItemName))
                    {
                        var isValidName = userInput.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;

                        bool exists;

                        if (isDirectory)
                        {
                            exists = Directory.Exists(Path.Combine(Directory.GetParent(oldItemPath).FullName, userInput));
                        }
                        else
                        {
                            exists = File.Exists(Path.Combine(oldItemPath, userInput));
                        }

                        if (isValidName && !exists)
                        {
                            return true;
                        }

                        if (exists)
                        {
                            OperationResult = $"{(isDirectory ? "Directory" : "File")} \"{userInput}\" already exists.";
                        }
                        else
                        {
                            OperationResult = $"Invalid {(isDirectory ? "directory" : "file")} name \"{userInput}\".";
                        }
                    }
                    else
                    {
                        OperationResult = $"You must enter different name than current \"{userInput}\" to rename {(isDirectory ? "directory" : "file")}.";
                    }

                    VS.MessageBox.ShowWarning(OperationResult);

                    return false;
                },
                out var newItemName
            );

            // If the user cancels the dialog, return.
            if (!result)
            {
                return;
            }

            if (isDirectory)
            {
                var newFolderPath = Path.Combine(Directory.GetParent(oldItemPath).FullName, newItemName);

                Directory.Move(oldItemPath, newFolderPath);
            }
            else
            {
                var newFilePath = Path.Combine(Path.GetDirectoryName(oldItemPath), newItemName);

                File.Move(oldItemPath, newFilePath);
            }
        }
    }
}
