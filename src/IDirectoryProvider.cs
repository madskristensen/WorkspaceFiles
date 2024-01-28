using System.Collections.Generic;
using System.IO;

namespace WorkspaceFiles
{
    internal interface IDirectoryProvider : IDisposable
    {
        event EventHandler<FileSystemEventArgs> DirectoryChanged;

        IEnumerable<DirectoryInfo> GetDirectories();
    }
}