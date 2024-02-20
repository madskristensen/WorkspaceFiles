using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    [Command(PackageIds.Rename)]
    internal sealed class RenameCommand : BaseCommand<RenameCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            var oldItemPath = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;
            var oldItemName = WorkspaceItemContextMenuController.CurrentItem.Info.Name;
            var result = TextInputDialog.Show(
                "Rename File",
                $"Enter the new name of the file for {oldItemName}.",
                oldItemName,
                userInput =>
                {
                    if (!userInput.Equals(oldItemName))
                    {
                        var isValidFileName = userInput.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;

                        var fileExists = File.Exists(Path.Combine(oldItemPath, userInput));

                        if (isValidFileName && !fileExists)
                        {
                            return true;
                        }
                    }

                    return false;
                },
                out var newItemName
            );

            // If the user cancels the dialog, return.
            if (!result) return;

            var newItemPath = Path.Combine(Path.GetDirectoryName(oldItemPath), newItemName);

            File.Move(oldItemPath, newItemPath);
        }
    }
}
