using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace WorkspaceFiles
{
    internal class WorkspaceRootNode : IAttachedCollectionSource, IDisposable
    {
        private readonly List<WorkspaceItemNode> _children = [];
        private bool _disposed = false;

        public WorkspaceRootNode(DirectoryInfo info)
        {
            _children.Add(new WorkspaceItemNode(this, info));
        }

        public object SourceItem => this;

        public bool HasItems => _children.Count > 0;

        public IEnumerable Items => _children;

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (WorkspaceItemNode child in _children)
                {
                    child.Dispose();
                }
            }

            _disposed = true;
        }
    }
}
