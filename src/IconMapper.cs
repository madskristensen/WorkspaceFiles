using System.IO;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace WorkspaceFiles
{
    internal static class IconMapper
    {
        private static IVsImageService2 _imageService => VS.GetRequiredService<SVsImageService, IVsImageService2>();

        public static ImageMoniker GetIcon(this FileSystemInfo info, bool isOpen)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (info is FileInfo file)
            {
                return _imageService.GetImageMonikerForFile(file.FullName);
            }

            switch (info.Name.ToLowerInvariant())
            {
                case ".docker":
                case "docker":
                case "dockerfiles":
                    return KnownMonikers.Docker;
                case ".git":
                case "patches":
                case "githooks":
                case ".githooks":
                case "submodules":
                case ".submodules":
                    return KnownMonikers.Git;
                case ".github": 
                    return KnownMonikers.GitHub;
                case ".vs":
                    return KnownMonikers.VisualStudio;
                case ".vscode":
                    return KnownMonikers.VisualStudioOnline;
            }

            return isOpen ? KnownMonikers.FolderOpened : KnownMonikers.FolderClosed;
        }
    }
}
