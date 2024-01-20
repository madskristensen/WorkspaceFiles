using Microsoft.Internal.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    [Command(PackageIds.ToggleWorkspace)]
    internal sealed class EnableWorkspaceCommand : BaseCommand<EnableWorkspaceCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            Command.Checked = !Command.Checked;
            General.Instance.Enabled = Command.Checked;
            General.Instance.Save();
            
            IAttachedCollectionService svc = VS.GetMefService<IAttachedCollectionService>();
            Solution sol = VS.Solutions.GetCurrentSolution();
            sol.GetItemInfo(out _, out _, out IVsHierarchyItem hierarchyItem);
            svc.GetOrCreateCollectionSource(hierarchyItem, KnownRelationships.Contains);
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = General.Instance.Enabled;
        }
    }
}
