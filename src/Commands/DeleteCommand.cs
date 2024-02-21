using System.IO;
using EnvDTE;

namespace WorkspaceFiles
{
    [Command(PackageIds.Delete)]
    internal sealed class DeleteCommand : BaseCommand<DeleteCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                DTE dte = await VS.GetRequiredServiceAsync<DTE, DTE>();

                if (dte.Solution.FindProjectItem(WorkspaceItemContextMenuController.CurrentItem.Info.FullName) is ProjectItem item)
                {
                    var proceed = await VS.MessageBox.ShowConfirmAsync("This file is part of a project. Do you wish to delete it?");

                    if (proceed)
                    {
                        item.Delete();

                        // Also delete from disk in case the project item was contained only in a Solution Folder
                        if (File.Exists(WorkspaceItemContextMenuController.CurrentItem.Info.FullName))
                        {
                            WorkspaceItemContextMenuController.CurrentItem.Info.Delete();
                        }
                    }
                }
                else
                {
                    if (Directory.Exists(WorkspaceItemContextMenuController.CurrentItem.Info.FullName))
                    {
                        Directory.Delete(WorkspaceItemContextMenuController.CurrentItem.Info.FullName, true);
                    }
                    else
                    {
                        WorkspaceItemContextMenuController.CurrentItem.Info.Delete();
                    }

                    object parent = WorkspaceItemContextMenuController.CurrentItem.SourceItem;
                    if (parent is WorkspaceItemNode parentNode)
                    {
                        // This is a hack to force the parent node to refresh its children since as per the documentation,
                        // if a FileSystemWatcher is monitoring the directory, the events are not raised in the parent directory.
                        // https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher.deleted?view=net-8.0#remarks
                        parentNode.ScheduleSyncChildren();
                    }
                }
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowErrorAsync(ex.Message);
            }
        }
    }
}
