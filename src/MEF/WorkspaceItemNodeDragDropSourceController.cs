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
            if (!items.OfType<WorkspaceItemNode>().Any())
            {
                return false;
            }

            var path = (items.FirstOrDefault() as WorkspaceItemNode)?.Info.FullName;

            DependencyObject dragSource = (Keyboard.FocusedElement as DependencyObject) ?? Application.Current.MainWindow;
            var dataObj = new DataObject(DataFormats.FileDrop, items.OfType<WorkspaceItemNode>().Select(i => i.Info.FullName).ToArray());
            DragDrop.DoDragDrop(dragSource, dataObj, DragDropEffects.Move);

            return true;
        }
    }
}