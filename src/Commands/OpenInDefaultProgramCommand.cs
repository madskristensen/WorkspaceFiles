using System.Diagnostics;

namespace WorkspaceFiles
{
    [Command(PackageIds.OpenInDefaultProgram)]
    internal sealed class OpenInDefaultProgramCommand : BaseCommand<OpenInDefaultProgramCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Enabled = WorkspaceItemContextMenuController.CurrentItems.Count == 1;
        }

        protected override void Execute(object sender, EventArgs e)
        {
            Process.Start(WorkspaceItemContextMenuController.CurrentItem.Info.FullName);
        }
    }
}
