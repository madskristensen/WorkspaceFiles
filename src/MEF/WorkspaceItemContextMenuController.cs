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
        private static IReadOnlyList<WorkspaceItemNode> _currentItems = [];

        /// <summary>
        /// Gets the first selected item. Use <see cref="CurrentItems"/> for multi-select scenarios.
        /// </summary>
        public static WorkspaceItemNode CurrentItem => _currentItems.Count > 0 ? _currentItems[0] : null;

        /// <summary>
        /// Gets all selected items for multi-select context menu operations.
        /// </summary>
        public static IReadOnlyList<WorkspaceItemNode> CurrentItems => _currentItems;

        public bool ShowContextMenu(IEnumerable<object> items, Point location)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _currentItems = [.. items.OfType<WorkspaceItemNode>()];

            if (_currentItems.Count == 0)
            {
                return false;
            }

            IVsUIShell shell = VS.GetRequiredService<SVsUIShell, IVsUIShell>();
            Guid guid = PackageGuids.WorkspaceFiles;

            var result = shell.ShowContextMenu(
                dwCompRole: 0,
                rclsidActive: ref guid,
                nMenuId: GetMenuFromNodeType(),
                pos: [new POINTS() { x = (short)location.X, y = (short)location.Y }],
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