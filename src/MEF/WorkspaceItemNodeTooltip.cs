using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Threading;

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
                // Create placeholder image control that will load asynchronously
                var imageControl = new Image
                {
                    MaxHeight = 100,
                    MaxWidth = 100
                };

                // Load image asynchronously to avoid blocking UI thread
                _ = LoadImageAsync(info.FullName, imageControl);

                return imageControl;
            }
            return null;
        }

        private static async Task LoadImageAsync(string imagePath, Image imageControl)
        {
            try
            {
                // Switch to background thread for I/O operation
                await TaskScheduler.Default;

                // Load image on background thread
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; // Performance optimization
                bitmap.UriSource = new Uri(imagePath);
                bitmap.EndInit();
                bitmap.Freeze(); // Make it thread-safe for UI thread access

                // Switch back to UI thread to update the control
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                imageControl.Source = bitmap;
            }
            catch
            {
                // Silently handle errors (e.g., corrupted images, access denied)
                // Could optionally show a placeholder icon
            }
        }

        private static bool IsImage(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return _imageExt.Contains(extension);
        }
    }
}
