using System.IO;
using Microsoft.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    [Command(PackageIds.NewFile)]
    internal sealed class NewFileCommand : BaseCommand<NewFileCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            var itemPath = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;
            var result = TextInputDialog.Show(
                "New File",
                "Enter the name of the new file. (Note that files are not automatically added into projects)",
                "NewFile.txt",
                input =>
                {
                    // Check if the file name is valid and does not exist.
                    var isValidFileName = input.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;
                    var fileExists = File.Exists(Path.Combine(itemPath, input));
                    return isValidFileName && !fileExists;
                },
                out var fileName
            );

            // If the user cancels the dialog, return.
            if (!result) return;

            // Create the new file.
            var filePath = Path.Combine(itemPath, fileName);
            File.WriteAllText(filePath, string.Empty);
        }
    }
}
