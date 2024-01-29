using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WorkspaceFiles
{
    internal class LocalSolutionProvider : IDirectoryProvider, IDisposable
    {
        private List<DirectoryInfo> _directories;

        public LocalSolutionProvider()
        {
            AddFolderCommand.AddFolderRequest += OnAddFolderRequested;
            RemoveFolderCommand.RemoveFolderRequest += OnRemoveFolderRequested;
        }

        private void OnRemoveFolderRequested(object sender, string directory)
        {
            if (_directories.Any(d => d.FullName == directory))
            {
                var info = new DirectoryInfo(directory);
                _directories.RemoveAll(d => d.FullName == directory);
                DirectoryChanged?.Invoke(this, new FileSystemEventArgs(WatcherChangeTypes.Deleted, info.FullName, info.Name));
            }
        }

        private void OnAddFolderRequested(object sender, string directory)
        {
            AddDirectory(new DirectoryInfo(directory));
        }

        public IEnumerable<DirectoryInfo> GetDirectories()
        {
            if (_directories == null)
            {
                _directories = [];

                if (General.Instance.Enabled)
                {
                    var sol = VS.Solutions.GetCurrentSolution()?.FullPath;
                    if (!string.IsNullOrEmpty(sol))
                    {
                        var dir = Path.GetDirectoryName(sol);
                        _directories.Add(new DirectoryInfo(dir));
                    }
                }
            }

            return _directories;
        }

        public void AddDirectory(DirectoryInfo info)
        {
            if (!_directories.Contains(info))
            {
                _directories.Add(info);
                DirectoryChanged?.Invoke(this, new FileSystemEventArgs(WatcherChangeTypes.Created, info.FullName, info.Name));
            }
        }

        public void Dispose()
        {
            AddFolderCommand.AddFolderRequest -= OnAddFolderRequested;
            RemoveFolderCommand.RemoveFolderRequest -= OnRemoveFolderRequested;
        }

        public event EventHandler<FileSystemEventArgs> DirectoryChanged;
    }
}
