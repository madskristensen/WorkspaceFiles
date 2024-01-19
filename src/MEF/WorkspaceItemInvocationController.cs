using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    internal class WorkspaceItemInvocationController : IInvocationController
    {
        public bool Invoke(IEnumerable<object> items, InputSource inputSource, bool preview)
        {
            foreach (WorkspaceItem item in items.OfType<WorkspaceItem>())
            {
                if (item.Info is FileInfo)
                {
                    if (preview)
                    {
                        VS.Documents.OpenInPreviewTabAsync(item.Info.FullName).FireAndForget();
                    }
                    else
                    {
                        VS.Documents.OpenAsync(item.Info.FullName).FireAndForget();
                    }
                }
            }

            return true;
        }
    }
}
