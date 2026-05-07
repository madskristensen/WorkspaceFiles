using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace WorkspaceFiles
{
    [Command(PackageIds.ExecuteFile)]
    internal sealed class ExecuteFileCommand : BaseCommand<ExecuteFileCommand>
    {
        private static readonly HashSet<string> _executableExtensions =
            [with(StringComparer.OrdinalIgnoreCase), ".ps1", ".bat", ".cmd", ".exe"];

        protected override void BeforeQueryStatus(EventArgs e)
        {
            WorkspaceItemNode item = WorkspaceItemContextMenuController.CurrentItem;
            var isSingleFileSelection = WorkspaceItemContextMenuController.CurrentItems.Count == 1
                && item?.Type == WorkspaceItemType.File;

            Command.Supported = true;
            Command.Visible = isSingleFileSelection
                && _executableExtensions.Contains(Path.GetExtension(item.Info.FullName));
        }

        protected override void Execute(object sender, EventArgs e)
        {
            var filePath = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;
            var workingDirectory = Path.GetDirectoryName(filePath);
            var extension = Path.GetExtension(filePath);

            ProcessStartInfo startInfo = extension.ToLowerInvariant() switch
            {
                ".ps1" => new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoExit -File \"{filePath}\"",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true,
                },
                ".bat" or ".cmd" => new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/K \"{filePath}\"",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true,
                },
                _ => new ProcessStartInfo
                {
                    FileName = filePath,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true,
                },
            };

            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                VS.MessageBox.ShowErrorAsync(ex.Message).FireAndForget();
            }
        }
    }
}
