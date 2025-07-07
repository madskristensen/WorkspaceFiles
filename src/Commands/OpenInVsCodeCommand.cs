using System.Diagnostics;
using System.IO;

namespace WorkspaceFiles
{
    [Command(PackageIds.OpenInVsCode)]
    internal sealed class OpenInVsCodeCommand : BaseCommand<OpenInVsCodeCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            var path = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;
            var isDirectory = Directory.Exists(path);

            var args = isDirectory ? "." : $"{path}";

            var start = new ProcessStartInfo()
            {
                FileName = $"cmd.exe",
                Arguments = $"/C code \"{args}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            if (isDirectory)
            {
                start.WorkingDirectory = path;
            }

            Process.Start(start);
        }
    }
}
