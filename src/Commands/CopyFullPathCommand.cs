using System.Windows;

namespace WorkspaceFiles
{
    [Command(PackageIds.CopyFullPath)]
    internal sealed class CopyFullPathCommand : BaseCommand<CopyFullPathCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Supported = true;
            Command.Enabled = WorkspaceItemContextMenuController.CurrentItems.Count == 1;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            Clipboard.SetText(WorkspaceItemContextMenuController.CurrentItem.Info.FullName);

            await VS.StatusBar.ShowMessageAsync("Full path copied to clipboard");
        }
    }
}
