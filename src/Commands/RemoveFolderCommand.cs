using System.IO;

namespace WorkspaceFiles
{
    [Command(PackageIds.RemoveFolder)]
    internal sealed class RemoveFolderCommand : BaseCommand<RemoveFolderCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            FileSystemInfo item = WorkspaceItemContextMenuController.CurrentItem?.Info;

            if (item != null)
            {
                RemoveFolderRequest?.Invoke(this, item.FullName);
            }
        }

        public static event EventHandler<string> RemoveFolderRequest;
    }
}
