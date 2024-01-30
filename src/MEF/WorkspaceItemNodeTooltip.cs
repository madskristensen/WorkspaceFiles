using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace WorkspaceFiles.MEF
{
    internal class WorkspaceItemNodeTooltip
    {
        private static readonly string[] _imageExt = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".gif", ".tif", ".tiff", ".ico", ".bmp", ".wmp"];
        public static object GetTooltip(WorkspaceItemNode node)
        {
            if (node.IsCut)
            {
                return "File is matching a pattern in the .gitignore file";
            }

            FileSystemInfo info = node.Info;

            return File.Exists(info.FullName) && IsImage(info.Name)
                ? new Image
                {
                    MaxHeight = 120,
                    MaxWidth = 120,
                    Source = new BitmapImage(new Uri(info.FullName))
                }
                : null;
        }

        private static bool IsImage(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return _imageExt.Contains(extension);
        }
    }
}
