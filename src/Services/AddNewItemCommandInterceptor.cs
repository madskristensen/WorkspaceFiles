using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace WorkspaceFiles.Services
{
    /// <summary>
    /// Intercepts the built-in "Add New Item" command (Ctrl+Shift+A) so that it creates a file via
    /// <see cref="NewFileCommand"/> when a WorkspaceFiles node (Root/Folder/File) is the current
    /// selection, while leaving the command completely untouched everywhere else (regular projects,
    /// solution folders, etc. keep the native "Add New Item" dialog).
    /// <para>
    /// A VSCT &lt;KeyBinding&gt; scoped to the Solution Explorer tool window GUID was tried first, but
    /// that scoping applies to the *entire* tool window, not to specific node types within it — it
    /// intercepted Ctrl+Shift+A for every item in Solution Explorer (including regular projects),
    /// breaking the native shortcut everywhere. A priority command target lets us inspect the live
    /// selection before deciding whether to handle the command ourselves, and otherwise reports the
    /// command as unsupported so normal routing to the built-in handler proceeds unaffected.
    /// </para>
    /// See madskristensen/WorkspaceFiles#28.
    /// </summary>
    internal sealed class AddNewItemCommandInterceptor : IOleCommandTarget, IDisposable
    {
        private static readonly Guid s_stdCmdSet97 = VSConstants.GUID_VSStandardCommandSet97;
        private const uint AddNewItemCmdId = (uint)VSConstants.VSStd97CmdID.AddNewItem;

        private static readonly Lazy<AddNewItemCommandInterceptor> _instance = new(() => new AddNewItemCommandInterceptor());

        public static AddNewItemCommandInterceptor Instance => _instance.Value;

        private IVsRegisterPriorityCommandTarget _registerService;
        private uint _cookie;

        private AddNewItemCommandInterceptor()
        {
        }

        /// <summary>
        /// Registers this instance as a priority command target. Must be called once, on the UI
        /// thread, during package initialization.
        /// </summary>
        public void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_registerService != null)
            {
                return;
            }

            _registerService = ServiceProvider.GlobalProvider.GetService(typeof(SVsRegisterPriorityCommandTarget)) as IVsRegisterPriorityCommandTarget;
            _registerService?.RegisterPriorityCommandTarget(0, this, out _cookie);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (!IsAddNewItemQuery(ref pguidCmdGroup, cCmds, prgCmds) || NewFileCommand.ResolveTargetFolder() == null)
            {
                // Not our command (or no WorkspaceFiles node selected): report unsupported so the
                // shell continues routing to the next target, ultimately the built-in handler.
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }

            prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
            return VSConstants.S_OK;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguidCmdGroup != s_stdCmdSet97 || nCmdID != AddNewItemCmdId)
            {
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }

            var targetFolder = NewFileCommand.ResolveTargetFolder();

            if (targetFolder == null)
            {
                // No WorkspaceFiles node selected: let the shell fall through to the built-in handler.
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            }

            NewFileCommand.CreateNewFileInteractive(targetFolder);
            return VSConstants.S_OK;
        }

        private static bool IsAddNewItemQuery(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds)
        {
            return pguidCmdGroup == s_stdCmdSet97 && cCmds == 1 && prgCmds != null && prgCmds[0].cmdID == AddNewItemCmdId;
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_registerService != null && _cookie != 0)
            {
                _registerService.UnregisterPriorityCommandTarget(_cookie);
                _cookie = 0;
            }
        }
    }
}
