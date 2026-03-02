using System.Diagnostics;
using System.IO;

namespace WorkspaceFiles
{
    [Command(PackageIds.OpenInTerminal)]
    internal sealed class OpenInTerminalCommand : BaseCommand<OpenInTerminalCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Enabled = WorkspaceItemContextMenuController.CurrentItems.Count == 1;
        }

        protected override void Execute(object sender, EventArgs e)
        {
            var path = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;

            if (!Directory.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var terminal = new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"-d \"{path}\"",
                WorkingDirectory = path,
                UseShellExecute = true,
            };

            if (!TryStart(terminal))
            {
                var fallback = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command \"Set-Location -LiteralPath '{path.Replace("'", "''")}'\"",
                    WorkingDirectory = path,
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
