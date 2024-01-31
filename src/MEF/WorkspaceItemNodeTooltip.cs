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

            if (File.Exists(info.FullName) && IsImage(info.Name))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(info.FullName);
                bitmap.EndInit();

                return new Image
                {
                    MaxHeight = 100,
                    MaxWidth = 100,
                    Source = bitmap
                };
            }
            return null;
        }

        private static bool IsImage(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return _imageExt.Contains(extension);
        }
    }
}
