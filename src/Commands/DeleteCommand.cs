namespace WorkspaceFiles
{
    [Command(PackageIds.Delete)]
    internal sealed class DeleteCommand : BaseCommand<DeleteCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                WorkspaceItemContextMenuController.CurrentItem.Info.Delete();
                WorkspaceItemContextMenuController.CurrentItem.IsCut = true;
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowErrorAsync(ex.Message);
            }
        }
    }
}
