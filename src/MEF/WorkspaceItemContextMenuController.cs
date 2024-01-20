using System.Collections.Generic;
using System.IO;
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

            var menuId = PackageIds.FileContextMenu;

            if (CurrentItem.IsRoot == true)
            {
                menuId = PackageIds.RootContextMenu;
            }
            else if (CurrentItem.Info  is DirectoryInfo)
            {
                menuId = PackageIds.FolderContextMenu;
            }

            IVsUIShell shell = VS.GetRequiredService<SVsUIShell, IVsUIShell>();
            Guid guid = PackageGuids.WorkspaceFiles;

            var result = shell.ShowContextMenu(
                dwCompRole: 0, 
                rclsidActive: ref guid, 
                nMenuId: menuId, 
                pos: new[] { new POINTS() { x = (short)location.X, y = (short)location.Y } }, 
                pCmdTrgtActive: null);

            return ErrorHandler.Succeeded(result);
        }
    }
}