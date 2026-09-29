using WorkspaceFiles.Services;

namespace WorkspaceFiles
{
    [Command(PackageIds.ViewHistory)]
    internal sealed class ViewHistoryCommand : BaseCommand<ViewHistoryCommand>
    {
        // Visual Studio's built-in Git "View History" command. It acts on the active document,
        // so the file is opened in the editor before the command is executed.
        private const string GitViewHistoryCommand = "Team.Git.ViewHistory";

        protected override void BeforeQueryStatus(EventArgs e)
        {
            WorkspaceItemNode item = WorkspaceItemContextMenuController.CurrentItem;
            var isSingleFileSelection = WorkspaceItemContextMenuController.CurrentItems.Count == 1
                && item?.Type == WorkspaceItemType.File;

            Command.Supported = true;
            Command.Visible = isSingleFileSelection
                && GitRepositoryDetector.TryFindGitRoot(item.Info.FullName, out _);
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var filePath = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;

            DocumentView documentView = await VS.Documents.OpenAsync(filePath);

            if (documentView == null || !await VS.Commands.ExecuteAsync(GitViewHistoryCommand))
            {
                await VS.MessageBox.ShowErrorAsync("Unable to show the Git history for this file. Make sure the file is part of the Git repository opened in Visual Studio.");
            }
        }
    }
}
