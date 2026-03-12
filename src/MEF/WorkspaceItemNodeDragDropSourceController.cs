using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    internal class WorkspaceItemNodeDragDropSourceController : IDragDropSourceController
    {
        public bool DoDragDrop(IEnumerable<object> items)
        {
            var nodes = items.OfType<WorkspaceItemNode>().ToArray();

            if (!nodes.Any())
            {
                return false;
            }

            var paths = nodes.Select(i => i.Info.FullName).ToArray();

            DependencyObject dragSource = (Keyboard.FocusedElement as DependencyObject) ?? Application.Current.MainWindow;
            var dataObj = new System.Windows.Forms.DataObject();
            var fileDropList = new StringCollection();
            fileDropList.AddRange(paths);

            // Provide multiple shell-compatible formats so Solution Explorer targets
            // (including Solution Folders) can recognize dragged file paths.
            dataObj.SetFileDropList(fileDropList);
            dataObj.SetData(DataFormats.FileDrop, paths);
            dataObj.SetData("FileNameW", paths);
            dataObj.SetData("FileName", paths);
            dataObj.SetData(DataFormats.UnicodeText, string.Join("\r\n", paths));

            // Solution Explorer solution-folder drops can require VS-specific formats.
            // These formats use the same DROPFILES payload shape as CF_HDROP.
            dataObj.SetData("CF_VSSTGPROJECTITEMS", BuildDropFilesPayload(paths));
            dataObj.SetData("CF_VSREFPROJECTITEMS", BuildDropFilesPayload(paths));

            DragDrop.DoDragDrop(dragSource, dataObj, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);

            return true;
        }

        private static MemoryStream BuildDropFilesPayload(string[] paths)
        {
            const int dropFilesHeaderSize = 20; // sizeof(DROPFILES)

            string files = string.Join("\0", paths) + "\0\0";
            byte[] filesBytes = Encoding.Unicode.GetBytes(files);
            byte[] payload = new byte[dropFilesHeaderSize + filesBytes.Length];

            // DROPFILES.pFiles offset to start of file list
            BitConverter.GetBytes(dropFilesHeaderSize).CopyTo(payload, 0);
            // pt.x (4), pt.y (8), fNC (12) remain 0
            // DROPFILES.fWide = TRUE at offset 16
            BitConverter.GetBytes(1).CopyTo(payload, 16);

            Buffer.BlockCopy(filesBytes, 0, payload, dropFilesHeaderSize, filesBytes.Length);

            return new MemoryStream(payload);
        }
    }
}