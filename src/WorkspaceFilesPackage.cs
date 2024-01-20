﻿global using System;
global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;

namespace WorkspaceFiles
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.WorkspaceFilesString)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), Vsix.Name, "General", 0, 0, true, SupportsProfiles = true, ProvidesLocalizedCategoryName = false)]
    [ProvideFileIcon(".gitignore", "KnownMonikers.DocumentExclude")]
    [ProvideFileIcon(".tfignore", "KnownMonikers.DocumentExclude")]
    [ProvideFileIcon(".editorconfig", "KnownMonikers.EditorConfigFile")]
    [ProvideFileIcon(".gitattributes", "KnownMonikers.ConfigurationFile")]
    public sealed class WorkspaceFilesPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
            
            // Setup ratings prompt
            General options = await General.GetLiveInstanceAsync();
            RatingPrompt prompt = new("MadsKristensen.WorkspaceBrowser", Vsix.Name, options);
            prompt.RegisterSuccessfulUsage();
        }
    }
}