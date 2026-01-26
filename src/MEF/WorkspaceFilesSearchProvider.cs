using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Utilities;

namespace WorkspaceFiles
{
    /// <summary>
    /// Provides search results for workspace file nodes in Solution Explorer.
    /// </summary>
    /// <remarks>
    /// This search provider enables Solution Explorer's search box to find files and folders
    /// in the Workspace Files tree. When a match is found, it uses the ContainedBy relationship
    /// to trace back to parent nodes so the full path can be displayed.
    /// 
    /// Performance optimizations:
    /// - Breadth-first search yields results at each level before going deeper
    /// - Parallel processing of subfolders at each level
    /// - Early termination when result limit is reached
    /// </remarks>
    [Export(typeof(ISearchProvider))]
    [Name(nameof(WorkspaceFilesSearchProvider))]
    [Order(Before = "GraphSearchProvider")]
    [method: ImportingConstructor]
    internal sealed class WorkspaceFilesSearchProvider(
        [Import] WorkspaceItemSourceProvider sourceProvider) : ISearchProvider
    {
        /// <summary>
        /// Maximum number of search results to return. Prevents performance issues in very large workspaces.
        /// </summary>
        private const int _maxSearchResults = 200;

        public void Search(IRelationshipSearchParameters parameters, Action<ISearchResult> resultAccumulator)
        {
            if (parameters == null || resultAccumulator == null)
            {
                return;
            }

            var searchPattern = parameters.SearchQuery?.SearchString;
            if (string.IsNullOrWhiteSpace(searchPattern))
            {
                return;
            }

            // Ensure the root node exists - this is needed for ContainedBy relationship to work
            WorkspaceRootNode rootNode = sourceProvider.RootNode;
            if (rootNode == null)
            {
                // Root node doesn't exist yet (tree not expanded)
                return;
            }

            if (!rootNode.HasItems)
            {
                return;
            }

            // Search through all workspace items using breadth-first parallel search
            System.Collections.IEnumerable items = rootNode.Items;
            if (items == null)
            {
                return;
            }

            SearchBreadthFirstParallel(items.OfType<WorkspaceItemNode>(), searchPattern, resultAccumulator);
        }

        /// <summary>
        /// Performs breadth-first search with parallel processing of folders at each level.
        /// This yields results faster by searching shallower levels first and processing
        /// multiple folders in parallel.
        /// </summary>
        private void SearchBreadthFirstParallel(
            IEnumerable<WorkspaceItemNode> rootItems,
            string searchPattern,
            Action<ISearchResult> resultAccumulator)
        {
            var resultCount = 0;

            // Queue of folders to process at the next level
            var currentLevel = new List<WorkspaceItemNode>(rootItems);

            while (currentLevel.Count > 0 && resultCount < _maxSearchResults)
            {
                // Collect results and subfolders from the current level in parallel
                var results = new ConcurrentBag<WorkspaceItemNode>();
                var nextLevel = new ConcurrentBag<WorkspaceItemNode>();

                // Process all folders at this level in parallel
                Parallel.ForEach(
                    currentLevel,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    (node, loopState) =>
                    {
                        // Check early termination
                        if (Volatile.Read(ref resultCount) >= _maxSearchResults)
                        {
                            loopState.Stop();
                            return;
                        }

                        // Check if this node matches
                        if (MatchesSearch(node.Text, searchPattern))
                        {
                            results.Add(node);
                        }

                        // If this is a folder, get its children for the next level
                        if (node.Type == WorkspaceItemType.Folder || node.Type == WorkspaceItemType.Root)
                        {
                            foreach (WorkspaceItemNode child in node.GetChildrenForSearch())
                            {
                                nextLevel.Add(child);
                            }
                        }
                    });

                // Emit results from this level (on the calling thread for thread safety)
                foreach (WorkspaceItemNode match in results)
                {
                    if (resultCount >= _maxSearchResults)
                    {
                        break;
                    }

                    SetupContainedByChain(match);
                    resultAccumulator(new WorkspaceSearchResult(match));
                    resultCount++;
                }

                // Move to the next level
                currentLevel = [.. nextLevel];
            }
        }

        /// <summary>
        /// Sets up the ContainedBy collection chain from the given node back to the solution.
        /// This pre-populates the parent relationship so VS doesn't need to call the source provider.
        /// </summary>
        private static void SetupContainedByChain(WorkspaceItemNode node)
        {
            // Walk up the parent chain, setting ContainedByCollection on each item
            object current = node;

            while (current != null)
            {
                if (current is WorkspaceItemNode itemNode)
                {
                    // Set the ContainedBy collection if not already set
                    itemNode.ContainedByCollection ??= new ContainedByCollection(itemNode, itemNode.ParentItem);
                    current = itemNode.ParentItem;
                }
                else if (current is WorkspaceRootNode rn)
                {
                    // Set the ContainedBy collection for the root node to point to solution
                    rn.ContainedByCollection ??= new ContainedByCollection(rn, rn.ParentItem);
                    // Stop - we've reached the root
                    break;
                }
                else
                {
                    // Unknown type, stop
                    break;
                }
            }
        }

        private static bool MatchesSearch(string text, string searchPattern)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Case-insensitive substring match (consistent with Solution Explorer behavior)
            return text.IndexOf(searchPattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// Represents a search result for a workspace file node.
    /// </summary>
    internal sealed class WorkspaceSearchResult(WorkspaceItemNode node) : ISearchResult
    {
        public object GetDisplayItem()
        {
            // Return the node itself as the display item - it implements ITreeDisplayItem
            return node;
        }
    }
}
