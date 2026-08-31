using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    [Command(PackageIds.NewFile)]
    internal sealed class NewFileCommand : BaseCommand<NewFileCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            var targetFolder = ResolveTargetFolder();

            Command.Visible = targetFolder != null;
            Command.Enabled = targetFolder != null;
        }

        /// <summary>
        /// Resolves the folder a new file should be created in from the current selection:
        /// a Root/Folder node is used directly, a File node resolves to its parent folder, and when
        /// multiple items are selected the first one is used. Returns null when the current selection
        /// contains no WorkspaceFiles node at all.
        /// </summary>
        internal static string ResolveTargetFolder()
        {
            IReadOnlyList<WorkspaceItemNode> items = WorkspaceItemContextMenuController.CurrentItems;

            if (items.Count == 0)
            {
                return null;
            }

            WorkspaceItemNode item = items[0];

            return ResolveFolderFor(item.Type, item.Info.FullName);
        }

        /// <summary>
        /// Pure resolution rule, extracted from <see cref="ResolveTargetFolder"/> so it can be unit
        /// tested without needing to construct a real <see cref="WorkspaceItemNode"/> (which, for
        /// <see cref="WorkspaceItemType.Root"/>, requires a live DTE/IVsHierarchyItem parent).
        /// A Root/Folder node's own path is the target folder; a File node's target folder is its parent.
        /// </summary>
        internal static string ResolveFolderFor(WorkspaceItemType type, string fullName)
        {
            return type == WorkspaceItemType.File ? Path.GetDirectoryName(fullName) : fullName;
        }

        protected override void Execute(object sender, EventArgs e)
        {
            var itemPath = ResolveTargetFolder();

            if (itemPath == null)
            {
                return;
            }

            CreateNewFileInteractive(itemPath);
        }

        /// <summary>
        /// Prompts for a file name and creates the new (empty) file inside <paramref name="targetFolder"/>.
        /// Shared by the context-menu "New File" command and <see cref="Services.AddNewItemCommandInterceptor"/>,
        /// which routes the built-in Ctrl+Shift+A "Add New Item" shortcut here when a WorkspaceFiles node is
        /// the current selection.
        /// </summary>
        internal static void CreateNewFileInteractive(string targetFolder)
        {
            var result = TextInputDialog.Show(
                "New File",
                "Enter the name of the new file. (Note that files are not automatically added into projects)",
                "NewFile.txt",
                input =>
                {
                    // Check if the file name is valid and does not exist.
                    var isValidFileName = input.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;
                    var fileExists = File.Exists(Path.Combine(targetFolder, input));
                    return isValidFileName && !fileExists;
                },
                out var fileName
            );

            // If the user cancels the dialog, return.
            if (!result) return;

            // Create the new file.
            var filePath = Path.Combine(targetFolder, fileName);
            File.WriteAllText(filePath, string.Empty);

            ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                await Task.Delay(200);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                SendKeys.SendWait("{RIGHT}");
            }, VsTaskRunContext.UIThreadIdlePriority).FireAndForget();
        }
    }
}
