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

            var start = CreateVsCodeStartInfo(args);

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

        private static ProcessStartInfo CreateVsCodeStartInfo(string arguments)
        {
            return new ProcessStartInfo
            {
                FileName = FindVsCodeExecutablePath(),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
        }

        private static string FindVsCodeExecutablePath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            var candidates = new[]
            {
                Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe"),
                Path.Combine(programFiles, "Microsoft VS Code", "Code.exe"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "code.exe";
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
