using System.Diagnostics;

namespace WorkspaceFiles
{
    [Command(PackageIds.OpenInExplorer)]
    internal sealed class OpenInExplorerCommand : BaseCommand<OpenInExplorerCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            WorkspaceItemNode item = WorkspaceItemContextMenuController.CurrentItem;

            var arg = $"\"{item.Info?.FullName}\"";

            if (item.Type == WorkspaceItemType.File)
            {
                arg = $"/select, \"{item.Info.FullName}\"";
            }

            Process.Start("explorer.exe", arg);

        }
    }
}
