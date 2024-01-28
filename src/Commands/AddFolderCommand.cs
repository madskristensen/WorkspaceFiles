using System.IO;
using System.Windows.Forms;
using EnvDTE;

namespace WorkspaceFiles
{
    [Command(PackageIds.AddFolder)]
    internal sealed class AddFolderCommand : BaseCommand<AddFolderCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            var sln = VS.Solutions.GetCurrentSolution();
            var dir = Path.GetDirectoryName(sln.FullPath);
            
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.SelectedPath = dir;

                var result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    AddFolderRequest?.Invoke(this, fbd.SelectedPath);
                }
            }
        }

        public static event EventHandler<string> AddFolderRequest;
    }
}
