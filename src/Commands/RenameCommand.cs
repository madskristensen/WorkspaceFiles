using EnvDTE;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace WorkspaceFiles
{
    [Command(PackageIds.Rename)]
    internal sealed class RenameCommand : BaseCommand<RenameCommand>
    {
        protected override void Execute(object sender, EventArgs e)
        {
            VS.MessageBox.Show("Not implemented yet");
            //WorkspaceItemNode item = WorkspaceItemContextMenuController.CurrentItem;
            //if (item.CanRename)
            //{
            //    item.BeginRename(item, (e) =>
            //    {
            //        return new RenameItemValidatorResult(item.Text);
            //    });
            //}
        }

        public class RenameItemValidatorResult : IRenameItemValidationResult
        {
            public RenameItemValidatorResult(string previousValue)
            {
                PreviousValue = previousValue;
            }
            public bool IsValid => true;

            public string Feedback => "ostehat";

            public string PreviousValue { get; }

            public string ProposedValue { get; }
        }

    }
}
