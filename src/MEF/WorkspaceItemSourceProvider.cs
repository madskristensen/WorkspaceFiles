using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Utilities;

namespace WorkspaceFiles
{
    [Export(typeof(IAttachedCollectionSourceProvider))]
    [Name(nameof(WorkspaceItemSource))]
    [Order]
    internal class WorkspaceItemSourceProvider : IAttachedCollectionSourceProvider
    {
        public IEnumerable<IAttachedRelationship> GetRelationships(object item)
        {
            yield return Relationships.Contains;
        }

        public IAttachedCollectionSource CreateCollectionSource(object item, string relationshipName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (relationshipName == KnownRelationships.Contains && !HierarchyUtilities.IsSolutionClosing)
            {
                if (item is IVsHierarchyItem hierarchyItem)
                {
                    if (hierarchyItem.CanonicalName?.EndsWith(".sln") == true)
                    {
                        var root = new DirectoryInfo(Path.GetDirectoryName(VS.Solutions.GetCurrentSolution().FullPath));
                        return new WorkspaceItemSource(null, root);
                    }
                }

                if (item is WorkspaceItem workspaceItem)
                {
                    return new WorkspaceItemSource(workspaceItem, workspaceItem.Info);
                }

                // This provider will also be given the opportunity to attach children to
                // items it previously returned. In this way, it may build up multiple
                // levels of items in Solution Explorer.
            }

            // KnownRelationships.ContainedBy will be observed during Solution Explorer search,
            // where each attached item reports its parent(s) so that they may be displayed in the tree.
            // Search occurs via a MEF export of Microsoft.Internal.VisualStudio.PlatformUI.ISearchProvider.

            return null;
        }
    }
}
