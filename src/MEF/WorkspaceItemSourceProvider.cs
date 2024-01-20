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

            if (!General.Instance.Enabled || relationshipName != KnownRelationships.Contains || HierarchyUtilities.IsSolutionClosing)
            {
                return null;
            }

            if (item is IVsHierarchyItem hierarchyItem)
            {
                if (hierarchyItem.CanonicalName?.EndsWith(".sln") == true)
                {
                    return new WorkspaceItemSource(null, GetRoot());
                }
            }

            if (item is WorkspaceItem workspaceItem)
            {
                return new WorkspaceItemSource(workspaceItem, workspaceItem.Info);
            }


            // KnownRelationships.ContainedBy will be observed during Solution Explorer search,
            // where each attached item reports its parent(s) so that they may be displayed in the tree.
            // Search occurs via a MEF export of Microsoft.Internal.VisualStudio.PlatformUI.ISearchProvider.

            return null;
        }

        private static DirectoryInfo GetRoot()
        {
            var solRoot = new DirectoryInfo(Path.GetDirectoryName(VS.Solutions.GetCurrentSolution().FullPath));
            DirectoryInfo currentRoot = new(solRoot.FullName);

            while (currentRoot != null)
            {
                var dotGit = Path.Combine(currentRoot.FullName, ".git");

                if (Directory.Exists(dotGit))
                {
                    return currentRoot;
                }

                currentRoot = currentRoot.Parent;
            }

            return solRoot;
        }
    }
}
