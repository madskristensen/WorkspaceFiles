using System.Diagnostics;
using System.IO;

namespace WorkspaceFiles
{
    [Command(PackageIds.OpenInExplorer)]
    internal sealed class OpenInExplorerCommand : BaseCommand<OpenInExplorerCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            WorkspaceItem item = WorkspaceItemContextMenuController.CurrentItem;

            var arg = $"\"{item.Info?.FullName}\"";

            if (item.Info == null)
            {
                Solution sol = await VS.Solutions.GetCurrentSolutionAsync();
                arg = $"\"{Path.GetDirectoryName(sol.FullPath)}\"";
            }
            else if (item.Info is FileInfo)
            {
                arg = $"/select, \"{item.Info.FullName}\"";
            }

            Process.Start("explorer.exe", arg);

        }
    }
}
