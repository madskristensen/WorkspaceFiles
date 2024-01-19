using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    internal class WorkspaceItemDragDropSourceController : IDragDropSourceController
    {
        public bool DoDragDrop(IEnumerable<object> selectedItems)
        {
            IEnumerable<WorkspaceItem> items = selectedItems.OfType<WorkspaceItem>();

            if (items.Any())
            {
                DependencyObject dragSource = (Keyboard.FocusedElement as DependencyObject) ?? Application.Current.MainWindow;
                DragDrop.DoDragDrop(dragSource, items, DragDropEffects.All);
                return true;
            }

            return false;
        }
    }
}