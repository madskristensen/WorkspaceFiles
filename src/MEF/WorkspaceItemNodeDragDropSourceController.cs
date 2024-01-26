using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    internal class WorkspaceItemNodeDragDropSourceController : IDragDropSourceController
    {
        public bool DoDragDrop(IEnumerable<object> items)
        {
            if (items.Count() > 1)
            {
                return false;
            }

            DependencyObject dragSource = (Keyboard.FocusedElement as DependencyObject) ?? Application.Current.MainWindow;
            DragDrop.DoDragDrop(dragSource, items.Single(), DragDropEffects.Move);

            return true;
        }
    }
}