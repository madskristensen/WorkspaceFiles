using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using DteProject = EnvDTE.Project;
using DteSolutionFolder = EnvDTE80.SolutionFolder;
using DteSolution = EnvDTE.Solution;

namespace WorkspaceFiles
{
    [Command(PackageIds.ConvertToSolutionFolder)]
    internal sealed class ConvertToSolutionFolderCommand : BaseCommand<ConvertToSolutionFolderCommand>
    {
        private const string SolutionFolderKind = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";
        private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ".vs",
            "bin",
            "obj",
        };

        private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cache",
            ".resources",
        };

        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Enabled = WorkspaceItemContextMenuController.CurrentItems.Count == 1
                && WorkspaceItemContextMenuController.CurrentItem?.Type == WorkspaceItemType.Folder;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (WorkspaceItemContextMenuController.CurrentItem?.Info is not DirectoryInfo sourceFolder || !sourceFolder.Exists)
            {
                return;
            }

            try
            {
                DTE dte = await VS.GetRequiredServiceAsync<DTE, DTE>();

                if (dte?.Solution == null || string.IsNullOrWhiteSpace(dte.Solution.FullName))
                {
                    await VS.MessageBox.ShowErrorAsync("No solution is currently open.");
                    return;
                }

                DteProject rootFolder = GetOrCreateTopLevelSolutionFolder(dte.Solution, sourceFolder.Name);
                HashSet<string> excludedPaths = GetExcludedPaths(dte);
                AddDirectoryToSolutionFolder(dte, rootFolder, sourceFolder.FullName, excludedPaths);

                await VS.StatusBar.ShowMessageAsync($"Added '{sourceFolder.Name}' as solution folders.");
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                await VS.MessageBox.ShowErrorAsync(ex.Message);
            }
        }

        private static DteProject GetOrCreateTopLevelSolutionFolder(DteSolution solution, string folderName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DteProject existing = EnumerateProjects(solution)
                .FirstOrDefault(p => IsSolutionFolderProject(p) && string.Equals(p.Name, folderName, StringComparison.OrdinalIgnoreCase));

            var solution2 = (Solution2)solution;
            return existing ?? solution2.AddSolutionFolder(folderName);
        }

        private static void AddDirectoryToSolutionFolder(DTE dte, DteProject solutionFolderProject, string directoryPath, HashSet<string> excludedPaths)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (string filePath in Directory.EnumerateFiles(directoryPath))
            {
                if (excludedPaths.Contains(Path.GetFullPath(filePath)))
                {
                    continue;
                }

                if (!ShouldIncludeFile(filePath))
                {
                    continue;
                }

                AddFileIfMissing(solutionFolderProject.ProjectItems, filePath);
            }

            foreach (string childDirectory in Directory.EnumerateDirectories(directoryPath))
            {
                if (ShouldSkipDirectory(childDirectory))
                {
                    continue;
                }

                string folderName = Path.GetFileName(childDirectory);
                DteProject subFolderProject = GetOrCreateSubFolderProject(solutionFolderProject, folderName);

                if (subFolderProject != null)
                {
                    AddDirectoryToSolutionFolder(dte, subFolderProject, childDirectory, excludedPaths);
                }
                else
                {
                    AddDirectoryToSolutionFolder(dte, solutionFolderProject, childDirectory, excludedPaths);
                }
            }
        }

        private static void AddFileIfMissing(ProjectItems projectItems, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string fileName = Path.GetFileName(filePath);

            foreach (ProjectItem existingItem in projectItems)
            {
                if (existingItem != null
                    && string.Equals(existingItem.Name, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            try
            {
                projectItems.AddFromFile(filePath);
            }
            catch (COMException)
            {
                // Some file types/paths are rejected as solution items by VS.
                // Skip those and continue importing remaining files.
            }
        }

        private static HashSet<string> GetExcludedPaths(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string solutionPath = dte?.Solution?.FullName;
            if (!string.IsNullOrWhiteSpace(solutionPath))
            {
                excluded.Add(Path.GetFullPath(solutionPath));

                string suoPath = Path.ChangeExtension(solutionPath, ".suo");
                if (!string.IsNullOrWhiteSpace(suoPath))
                {
                    excluded.Add(Path.GetFullPath(suoPath));
                }
            }

            foreach (DteProject project in EnumerateProjects(dte.Solution))
            {
                if (!string.IsNullOrWhiteSpace(project?.FullName))
                {
                    excluded.Add(Path.GetFullPath(project.FullName));
                }
            }

            return excluded;
        }

        private static DteProject GetOrCreateSubFolderProject(DteProject parentSolutionFolderProject, string folderName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (ProjectItem item in parentSolutionFolderProject.ProjectItems)
            {
                if (!string.Equals(item.Name, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (item.SubProject != null && IsSolutionFolderProject(item.SubProject))
                {
                    return item.SubProject;
                }
            }

            if (parentSolutionFolderProject.Object is DteSolutionFolder solutionFolder)
            {
                try
                {
                    return solutionFolder.AddSolutionFolder(folderName);
                }
                catch (NotImplementedException)
                {
                    return null;
                }
            }

            try
            {
                ProjectItem item = parentSolutionFolderProject.ProjectItems.AddFolder(folderName);
                return item?.SubProject;
            }
            catch (NotImplementedException)
            {
                return null;
            }
        }

        private static bool ShouldSkipDirectory(string directoryPath)
        {
            string name = Path.GetFileName(directoryPath);
            return ExcludedDirectoryNames.Contains(name);
        }

        private static bool ShouldIncludeFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return !ExcludedExtensions.Contains(extension);
        }

        private static bool IsSolutionFolderProject(DteProject project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return project != null && string.Equals(project.Kind, SolutionFolderKind, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<DteProject> EnumerateProjects(DteSolution solution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (solution?.Projects == null)
            {
                yield break;
            }

            foreach (DteProject project in solution.Projects)
            {
                if (project == null)
                {
                    continue;
                }

                yield return project;
            }
        }
    }
}