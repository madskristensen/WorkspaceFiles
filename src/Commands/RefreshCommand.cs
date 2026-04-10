namespace WorkspaceFiles
{
    [Command(PackageIds.Refresh)]
    internal sealed class RefreshCommand : BaseCommand<RefreshCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            WorkspaceItemNode item = WorkspaceItemContextMenuController.CurrentItem;

            if (item != null)
            {
                await item.RefreshAsync();
            }
        }
    }
}
