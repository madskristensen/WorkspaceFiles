using System.Diagnostics;
using System.IO;

namespace WorkspaceFiles
{
    [Command(PackageIds.OpenInTerminal)]
    internal sealed class OpenInTerminalCommand : BaseCommand<OpenInTerminalCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            WorkspaceItemNode item = WorkspaceItemContextMenuController.CurrentItem;
            var path = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;

            if (!Directory.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }

            var exe = "cmd.exe";
            var args = $"/c wt.exe -d \"{path}\"";

            var pi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(pi);

        }
    }
}
