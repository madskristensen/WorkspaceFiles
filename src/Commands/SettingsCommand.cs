namespace WorkspaceFiles
{
    [Command(PackageIds.Settings)]
    internal sealed class SettingsCommand : BaseCommand<SettingsCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            VsShellUtilities.ShowToolsOptionsPage<OptionsProvider.GeneralOptions>();
        }
    }
}
