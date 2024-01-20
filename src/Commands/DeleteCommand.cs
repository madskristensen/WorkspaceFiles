namespace WorkspaceFiles
{
    [Command(PackageIds.Delete)]
    internal sealed class DeleteCommand : BaseCommand<DeleteCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            //WorkspaceItemContextMenuController.CurrentItem.IsCut = true;
            VS.MessageBox.Show("Not implemented yet");
        }
    }
}
