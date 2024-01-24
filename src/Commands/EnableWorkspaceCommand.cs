using Microsoft.Internal.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    [Command(PackageIds.ToggleWorkspace)]
    internal sealed class EnableWorkspaceCommand : BaseCommand<EnableWorkspaceCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            Command.Checked = !Command.Checked;
            General options = await General.GetLiveInstanceAsync();
            options.Enabled = Command.Checked;
            await options.SaveAsync();
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Visible = HierarchyUtilities.IsSolutionOpen;
            Command.Checked = General.Instance.Enabled;
        }
    }
}
