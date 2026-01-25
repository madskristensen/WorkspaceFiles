using System.IO;
using Microsoft.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    [Command(PackageIds.NewFolder)]
    internal sealed class NewFolderCommand : BaseCommand<NewFolderCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Enabled = WorkspaceItemContextMenuController.CurrentItems.Count == 1;
        }

        protected override void Execute(object sender, EventArgs e)
        {
            var itemPath = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;
            var result = TextInputDialog.Show(
                "New Folder",
                "Enter the name of the new folder. (Note that folders are not automatically added into projects)",
                "NewFolder",
                input =>
                {
                    // Check if the folder name is valid and does not exist. 
                    // Note that we use Path.GetInvalidFileNameChars() instead of Path.GetInvalidPathChars()
                    // so we avoid creating a folder that is a subfolder.
                    var isValidName = input.IndexOfAny(Path.GetInvalidPathChars()) == -1;
                    var folderExists = Directory.Exists(Path.Combine(itemPath, input));
                    return isValidName && !folderExists;
                },
                out var folderName
            );

            // If the user cancels the dialog, return.
            if (!result) return;

            // Create the new folder.
            var folderPath = Path.Combine(itemPath, folderName);
            Directory.CreateDirectory(folderPath);
        }
    }
}