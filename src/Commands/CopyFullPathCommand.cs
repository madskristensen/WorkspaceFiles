﻿using System.Windows;

namespace WorkspaceFiles
{
    [Command(PackageIds.CopyFullPath)]
    internal sealed class CopyFullPathCommand : BaseCommand<CopyFullPathCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            Clipboard.SetText(WorkspaceItemContextMenuController.CurrentItem.Info.FullName);

            await VS.StatusBar.ShowMessageAsync("Full path copied to clipboard");
        }
    }
}
