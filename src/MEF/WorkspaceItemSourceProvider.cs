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
                if (hierarchyItem.Parent == null && VS.Solutions.GetCurrentSolution() is Solution solution && TryGetRoot(solution, out var root))
                {
                    return new WorkspaceItemSource(null, root);
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

        private static bool TryGetRoot(Solution solution, out DirectoryInfo solRoot)
        {
            solRoot = null;

            if (string.IsNullOrEmpty(solution?.FullPath))
            {
                return false;
            }

            solRoot = new DirectoryInfo(Path.GetDirectoryName(solution.FullPath));
            DirectoryInfo currentRoot = new(solRoot.FullName);

            while (currentRoot != null)
            {
                var dotGit = Path.Combine(currentRoot.FullName, ".git");

                if (Directory.Exists(dotGit))
                {
                    solRoot = currentRoot;
                    return true;
                }

                currentRoot = currentRoot.Parent;
            }

            return true;
        }
    }
}
