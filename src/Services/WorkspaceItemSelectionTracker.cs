using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace WorkspaceFiles.Services
{
    /// <summary>
    /// Tracks the live selection of <see cref="WorkspaceItemNode"/> objects in Solution Explorer.
    /// <para>
    /// This exists because <see cref="WorkspaceItemContextMenuController.CurrentItems"/> was only ever
    /// updated when the right-click context menu was shown, which means keyboard-only command invocation
    /// (e.g. a keybinding like Ctrl+Shift+A) could never see the actual selection. This tracker subscribes
    /// to <see cref="IVsMonitorSelection"/>, which Solution Explorer uses to publish selection changes for
    /// all node types, including the non-hierarchy attached-collection nodes used by this extension.
    /// </para>
    /// </summary>
    internal sealed class WorkspaceItemSelectionTracker : IVsSelectionEvents, IDisposable
    {
        private static readonly Lazy<WorkspaceItemSelectionTracker> _instance = new(() => new WorkspaceItemSelectionTracker());

        public static WorkspaceItemSelectionTracker Instance => _instance.Value;

        private IVsMonitorSelection _monitorSelection;
        private uint _cookie;

        private WorkspaceItemSelectionTracker()
        {
        }

        /// <summary>
        /// Advises for selection change events. Must be called once, on the UI thread, during package
        /// initialization.
        /// </summary>
        public void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_monitorSelection != null)
            {
                return;
            }

            _monitorSelection = ServiceProvider.GlobalProvider.GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            _monitorSelection?.AdviseSelectionEvents(this, out _cookie);
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld,
            IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            // This callback is invoked by the shell on the UI thread.
            ThreadHelper.ThrowIfNotOnUIThread();

            WorkspaceItemContextMenuController.SetCurrentItems(ExtractWorkspaceItems(pSCNew));
            return VSConstants.S_OK;
        }

        private static IReadOnlyList<WorkspaceItemNode> ExtractWorkspaceItems(ISelectionContainer container)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (container == null)
            {
                return [];
            }

            try
            {
                if (ErrorHandler.Failed(container.CountObjects((uint)Constants.GETOBJS_SELECTED, out var count)) || count == 0)
                {
                    return [];
                }

                var objects = new object[count];
                if (ErrorHandler.Failed(container.GetObjects((uint)Constants.GETOBJS_SELECTED, count, objects)))
                {
                    return [];
                }

                return [.. objects.OfType<WorkspaceItemNode>()];
            }
            catch (Exception)
            {
                // Defensive: some selection containers may not support GetObjects for every selection kind.
                return [];
            }
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew) => VSConstants.S_OK;

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) => VSConstants.S_OK;

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_monitorSelection != null && _cookie != 0)
            {
                _monitorSelection.UnadviseSelectionEvents(_cookie);
                _cookie = 0;
            }
        }
    }
}
