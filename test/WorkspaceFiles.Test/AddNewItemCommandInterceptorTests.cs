using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceFiles.Services;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace WorkspaceFiles.Test
{
    [TestClass]
    public class AddNewItemCommandInterceptorTests
    {
        private static readonly Guid s_stdCmdSet97 = VSConstants.GUID_VSStandardCommandSet97;
        private const uint AddNewItemCmdId = (uint)VSConstants.VSStd97CmdID.AddNewItem;
        private static readonly Guid s_unrelatedGuid = new Guid("11111111-1111-1111-1111-111111111111");

        [TestCleanup]
        public void TearDown()
        {
            // WorkspaceItemContextMenuController.CurrentItems is process-global static state;
            // reset it so tests don't leak selection into one another.
            WorkspaceItemContextMenuController.SetCurrentItems(null);
        }

        // --- QueryStatus ---

        [TestMethod]
        [Description("A command group other than the standard 97 set must never be claimed, regardless of selection.")]
        public void WhenCommandGroupDoesNotMatchThenQueryStatusReportsNotSupported()
        {
            WorkspaceItemContextMenuController.SetCurrentItems(new[] { CreateFolderNode() });
            var cmdGroup = s_unrelatedGuid;
            var cmds = new[] { new OLECMD { cmdID = AddNewItemCmdId } };

            var hr = AddNewItemCommandInterceptor.Instance.QueryStatus(ref cmdGroup, 1, cmds, IntPtr.Zero);

            Assert.AreEqual((int)OleConstants.OLECMDERR_E_NOTSUPPORTED, hr);
        }

        [TestMethod]
        [Description("A command id other than AddNewItem within the standard 97 set must never be claimed.")]
        public void WhenCommandIdDoesNotMatchThenQueryStatusReportsNotSupported()
        {
            WorkspaceItemContextMenuController.SetCurrentItems(new[] { CreateFolderNode() });
            var cmdGroup = s_stdCmdSet97;
            var cmds = new[] { new OLECMD { cmdID = AddNewItemCmdId + 1 } };

            var hr = AddNewItemCommandInterceptor.Instance.QueryStatus(ref cmdGroup, 1, cmds, IntPtr.Zero);

            Assert.AreEqual((int)OleConstants.OLECMDERR_E_NOTSUPPORTED, hr);
        }

        [TestMethod]
        [Description("Add New Item must not be claimed when no WorkspaceFiles node is selected, so the shell falls through to the built-in handler.")]
        public void WhenNoWorkspaceItemIsSelectedThenQueryStatusReportsNotSupported()
        {
            WorkspaceItemContextMenuController.SetCurrentItems(new WorkspaceItemNode[0]);
            var cmdGroup = s_stdCmdSet97;
            var cmds = new[] { new OLECMD { cmdID = AddNewItemCmdId } };

            var hr = AddNewItemCommandInterceptor.Instance.QueryStatus(ref cmdGroup, 1, cmds, IntPtr.Zero);

            Assert.AreEqual((int)OleConstants.OLECMDERR_E_NOTSUPPORTED, hr);
        }

        [TestMethod]
        [Description("Add New Item must be claimed as supported+enabled when a WorkspaceFiles node is selected.")]
        public void WhenWorkspaceItemIsSelectedThenQueryStatusReportsSupportedAndEnabled()
        {
            WorkspaceItemContextMenuController.SetCurrentItems(new[] { CreateFolderNode() });
            var cmdGroup = s_stdCmdSet97;
            var cmds = new[] { new OLECMD { cmdID = AddNewItemCmdId } };

            var hr = AddNewItemCommandInterceptor.Instance.QueryStatus(ref cmdGroup, 1, cmds, IntPtr.Zero);

            Assert.AreEqual(VSConstants.S_OK, hr);
            var expectedFlags = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
            Assert.AreEqual(expectedFlags, cmds[0].cmdf);
        }

        // --- Exec (only the fall-through paths: the "handled" path pops a real modal dialog) ---

        [TestMethod]
        [Description("A command group other than the standard 97 set must never be executed, regardless of selection.")]
        public void WhenCommandGroupDoesNotMatchThenExecReportsNotSupported()
        {
            WorkspaceItemContextMenuController.SetCurrentItems(new[] { CreateFolderNode() });
            var cmdGroup = s_unrelatedGuid;

            var hr = AddNewItemCommandInterceptor.Instance.Exec(ref cmdGroup, AddNewItemCmdId, 0, IntPtr.Zero, IntPtr.Zero);

            Assert.AreEqual((int)OleConstants.OLECMDERR_E_NOTSUPPORTED, hr);
        }

        [TestMethod]
        [Description("A command id other than AddNewItem within the standard 97 set must never be executed.")]
        public void WhenCommandIdDoesNotMatchThenExecReportsNotSupported()
        {
            WorkspaceItemContextMenuController.SetCurrentItems(new[] { CreateFolderNode() });
            var cmdGroup = s_stdCmdSet97;

            var hr = AddNewItemCommandInterceptor.Instance.Exec(ref cmdGroup, AddNewItemCmdId + 1, 0, IntPtr.Zero, IntPtr.Zero);

            Assert.AreEqual((int)OleConstants.OLECMDERR_E_NOTSUPPORTED, hr);
        }

        [TestMethod]
        [Description("Add New Item must fall through to the built-in handler when no WorkspaceFiles node is selected.")]
        public void WhenNoWorkspaceItemIsSelectedThenExecReportsNotSupported()
        {
            WorkspaceItemContextMenuController.SetCurrentItems(new WorkspaceItemNode[0]);
            var cmdGroup = s_stdCmdSet97;

            var hr = AddNewItemCommandInterceptor.Instance.Exec(ref cmdGroup, AddNewItemCmdId, 0, IntPtr.Zero, IntPtr.Zero);

            Assert.AreEqual((int)OleConstants.OLECMDERR_E_NOTSUPPORTED, hr);
        }

        private static WorkspaceItemNode CreateFolderNode()
        {
            return new WorkspaceItemNode(
                parent: null,
                new System.IO.DirectoryInfo(@"C:\repo\src"),
                ignoreList: null,
                globbingMatcher: null,
                createWatcher: false);
        }
    }
}
