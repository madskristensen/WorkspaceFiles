using System.Collections.Generic;
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

            IReadOnlyList<WorkspaceItemNode> selectedItems = WorkspaceItemContextMenuController.CurrentItems;

            if (selectedItems.Count == 0)
            {
                return;
            }

            try
            {
                DTE dte = await VS.GetRequiredServiceAsync<DTE, DTE>();

                // Check if any items are part of a project
                var projectItems = new List<(WorkspaceItemNode node, ProjectItem projectItem)>();
                var nonProjectItems = new List<WorkspaceItemNode>();

                foreach (WorkspaceItemNode node in selectedItems)
                {
                    if (dte.Solution.FindProjectItem(node.Info.FullName) is ProjectItem projectItem)
                    {
                        projectItems.Add((node, projectItem));
                    }
                    else
                    {
                        nonProjectItems.Add(node);
                    }
                }

                // Handle project items with confirmation
                if (projectItems.Count > 0)
                {
                    var message = projectItems.Count == 1
                        ? "This file is part of a project. Do you wish to delete it?"
                        : $"{projectItems.Count} files are part of a project. Do you wish to delete them?";

                    var proceed = await VS.MessageBox.ShowConfirmAsync(message);

                    if (proceed)
                    {
                        foreach ((WorkspaceItemNode node, ProjectItem projectItem) in projectItems)
                        {
                            projectItem.Delete();

                            // Also delete from disk in case the project item was contained only in a Solution Folder
                            if (File.Exists(node.Info.FullName))
                            {
                                node.Info.Delete();
                            }
                        }
                    }
                }

                // Handle non-project items
                HashSet<WorkspaceItemNode> parentsToRefresh = [];

                foreach (WorkspaceItemNode node in nonProjectItems)
                {
                    if (Directory.Exists(node.Info.FullName))
                    {
                        Directory.Delete(node.Info.FullName, true);
                    }
                    else if (File.Exists(node.Info.FullName))
                    {
                        node.Info.Delete();
                    }

                    if (node.ParentItem is WorkspaceItemNode parentNode)
                    {
                        parentsToRefresh.Add(parentNode);
                    }
                }

                // Refresh parent nodes once after all deletions
                foreach (WorkspaceItemNode parentNode in parentsToRefresh)
                {
                    // This is a hack to force the parent node to refresh its children since as per the documentation,
                    // if a FileSystemWatcher is monitoring the directory, the events are not raised in the parent directory.
                    // https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher.deleted?view=net-8.0#remarks
                    parentNode.RefreshAsync().FireAndForget();
                }
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowErrorAsync(ex.Message);
            }
        }
    }
}
