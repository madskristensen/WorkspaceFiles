using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities;

namespace WorkspaceFiles
{
    [Export(typeof(IAttachedCollectionSourceProvider))]
    [Name(nameof(WorkspaceItemNode))]
    internal class WorkspaceItemSourceProvider : IAttachedCollectionSourceProvider
    {
        private WorkspaceRootNode _rootNode;

        public WorkspaceItemSourceProvider()
        {
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnBeforeCloseSolution += SolutionEvents_OnAfterCloseSolution;
        }

        private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
        {
            _rootNode?.Dispose();
        }

        public IEnumerable<IAttachedRelationship> GetRelationships(object item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsSolutionNode(item) || item is WorkspaceItemNode)
            {
                // Given the solution node or one of our nodes, we can provide children
                yield return Relationships.Contains;
            }

            //if (item is WorkspaceItemNode)
            //{
            //    // Given one of our nodes, we can return the item that contains it
            //    yield return Relationships.ContainedBy;
            //}
        }

        public IAttachedCollectionSource CreateCollectionSource(object item, string relationshipName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (HierarchyUtilities.IsSolutionClosing)
            {
                return null;
            }

            if (relationshipName == KnownRelationships.Contains)
            {
                if (IsSolutionNode(item))
                {
                    _rootNode?.Dispose();
                    _rootNode = new WorkspaceRootNode();
                    return _rootNode;
                }
                else if (item is IAttachedCollectionSource source and (WorkspaceRootNode or WorkspaceItemNode))
                {
                    return source;
                }
            }

            return null;
        }

        private static bool TryGetRoot(out DirectoryInfo solRoot)
        {
            solRoot = null;
            Solution solution = VS.Solutions.GetCurrentSolution();

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

        private bool IsSolutionNode(object item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (item is IVsHierarchyItem hierarchyItem)
            {
                IVsHierarchyItemIdentity identity = hierarchyItem.HierarchyIdentity;
                
                return identity?.Hierarchy is IVsSolution sol && identity.ItemID == (uint)VSConstants.VSITEMID.Root && VS.Solutions.GetCurrentSolution().FullPath != null;
            }

            return false;
        }
    }
}
