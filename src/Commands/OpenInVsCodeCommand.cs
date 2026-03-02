using System.Diagnostics;
using System.IO;

namespace WorkspaceFiles
{
    [Command(PackageIds.OpenInVsCode)]
    internal sealed class OpenInVsCodeCommand : BaseCommand<OpenInVsCodeCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Enabled = WorkspaceItemContextMenuController.CurrentItems.Count == 1;
        }

        protected override void Execute(object sender, EventArgs e)
        {
            var path = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;
            var isDirectory = Directory.Exists(path);

            var args = isDirectory ? "." : $"\"{path}\"";

            var start = new ProcessStartInfo()
            {
                FileName = "code",
                Arguments = args,
                UseShellExecute = true,
            };

            if (isDirectory)
            {
                start.WorkingDirectory = path;
            }
            else
            {
                start.WorkingDirectory = Path.GetDirectoryName(path);
            }

            if (!TryStart(start))
            {
                var fallback = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = isDirectory ? $"\"{path}\"" : $"/select, \"{path}\"",
                    UseShellExecute = true,
                };
                _ = TryStart(fallback);
            }
        }

        private static bool TryStart(ProcessStartInfo startInfo)
        {
            try
            {
                _ = Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
