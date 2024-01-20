using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace WorkspaceFiles
{
    internal class WorkspaceItemContextMenuController : IContextMenuController
    {
        public static WorkspaceItem CurrentItem { get; private set; }

        public bool ShowContextMenu(IEnumerable<object> items, Point location)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CurrentItem = items.OfType<WorkspaceItem>().FirstOrDefault();

            if (CurrentItem == null)
            {
                return false;
            }

            IVsUIShell shell = VS.GetRequiredService<SVsUIShell, IVsUIShell>();
            Guid guid = PackageGuids.WorkspaceFiles;

            var result = shell.ShowContextMenu(
                dwCompRole: 0,
                rclsidActive: ref guid,
                nMenuId: GetMenuFromNodeType(),
                pos: new[] { new POINTS() { x = (short)location.X, y = (short)location.Y } },
                pCmdTrgtActive: null);

            return ErrorHandler.Succeeded(result);
        }

        private static int GetMenuFromNodeType()
        {
            return CurrentItem.Type switch
            {
                WorkspaceItemType.File => PackageIds.FileContextMenu,
                WorkspaceItemType.Folder => PackageIds.FolderContextMenu,
                WorkspaceItemType.Root => PackageIds.RootContextMenu,
                _ => throw new NotImplementedException("WorkspaceItemType not supported"),
            };
        }
    }
}