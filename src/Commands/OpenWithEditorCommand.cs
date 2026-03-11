using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace WorkspaceFiles
{
    [Command(PackageIds.OpenWithEditor)]
    internal sealed class OpenWithEditorCommand : BaseCommand<OpenWithEditorCommand>
    {
        private const string TextViewLogicalViewRegistryValueName = "{7651a703-06e5-11d1-8ebd-00a0c90f26ea}";

        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Enabled = WorkspaceItemContextMenuController.CurrentItems.Count == 1
                && WorkspaceItemContextMenuController.CurrentItem?.Info is FileInfo;
        }

        protected override void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string filePath = WorkspaceItemContextMenuController.CurrentItem.Info.FullName;
            List<EditorInfo> editors = GetAvailableEditors();

            if (editors.Count == 0)
            {
                return;
            }

            OpenWithDialog dialog = new(editors);
            bool? dialogResult = dialog.ShowModal();

            if (dialogResult != true || dialog.SelectedEditor == null)
            {
                return;
            }

            Guid editorGuid = dialog.SelectedEditor.EditorGuid;
            Guid logicalView = VSConstants.LOGVIEWID.TextView_guid;

            VsShellUtilities.OpenDocumentWithSpecificEditor(ServiceProvider.GlobalProvider, filePath, editorGuid, logicalView, out _, out _, out IVsWindowFrame frame);
            frame?.Show();
        }

        /// <summary>
        /// Reads editors from the VS Config hive under Config\Editors and returns those
        /// with CommonPhysicalViewAttributes set to 3 (supports physical views).
        /// </summary>
        private static List<EditorInfo> GetAvailableEditors()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            List<EditorInfo> editors = [];

            using RegistryKey configRoot = VSRegistry.RegistryRoot(ServiceProvider.GlobalProvider, __VsLocalRegistryType.RegType_Configuration, writable: false);

            if (configRoot == null)
            {
                return editors;
            }

            using RegistryKey editorsKey = configRoot.OpenSubKey("Editors");

            if (editorsKey == null)
            {
                return editors;
            }

            IVsShell shell = VS.GetRequiredService<SVsShell, IVsShell>();

            foreach (string subKeyName in editorsKey.GetSubKeyNames())
            {
                if (!Guid.TryParse(subKeyName, out Guid editorGuid))
                {
                    continue;
                }

                using RegistryKey editorKey = editorsKey.OpenSubKey(subKeyName);

                if (editorKey == null)
                {
                    continue;
                }

                object commonPhysicalViewAttr = editorKey.GetValue("CommonPhysicalViewAttributes");

                if (commonPhysicalViewAttr == null || Convert.ToInt32(commonPhysicalViewAttr) is not 2 and not 3)
                {
                    continue;
                }

                using RegistryKey logicalViewsKey = editorKey.OpenSubKey("LogicalViews");

                if (logicalViewsKey == null)
                {
                    continue;
                }

                object logicalViewValue = logicalViewsKey.GetValue(TextViewLogicalViewRegistryValueName);

                if (logicalViewValue is not string logicalViewData || logicalViewData.Length > 0)
                {
                    continue;
                }

                string displayName = editorKey.GetValue("DisplayName") as string;

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                // DisplayName may be a resource reference like "#1100" — resolve it via the Package DLL
                displayName = ResolveDisplayName(displayName, editorKey, shell);

                if (displayName.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                editors.Add(new EditorInfo(editorGuid, displayName));
            }

            editors.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            return editors;
        }

        /// <summary>
        /// Resolves resource-reference display names (e.g. "#1100") to their localized string
        /// using <see cref="IVsShell.LoadPackageString"/>. Returns the original value if resolution fails.
        /// </summary>
        private static string ResolveDisplayName(string displayName, RegistryKey editorKey, IVsShell shell)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!displayName.StartsWith("#", StringComparison.Ordinal))
            {
                return displayName;
            }

            if (!uint.TryParse(displayName.Substring(1), out uint resourceId))
            {
                return displayName;
            }

            string packageGuidString = editorKey.GetValue("Package") as string;

            if (string.IsNullOrWhiteSpace(packageGuidString) || !Guid.TryParse(packageGuidString, out Guid packageGuid))
            {
                return displayName;
            }

            int hr = shell.LoadPackageString(ref packageGuid, resourceId, out string resolved);

            return ErrorHandler.Succeeded(hr) && !string.IsNullOrWhiteSpace(resolved) ? resolved : displayName;
        }
    }
}
