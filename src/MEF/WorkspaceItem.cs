using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace WorkspaceFiles
{
    internal class WorkspaceItem :
        ITreeDisplayItem,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IBrowsablePattern,
        IInteractionPatternProvider,
        IContextMenuPattern,
        IInvocationPattern
    {
        private IVsImageService2 _imageService => VS.GetRequiredService<SVsImageService, IVsImageService2>();

        public WorkspaceItem(FileSystemInfo info, bool isRoot = false)
        {
            Info = info;
            _isRoot = isRoot;
        }


        public FileSystemInfo Info { get; }

        public string Text => _isRoot ? "Workspace" : Info.Name;

        public string ToolTipText => "";

        public string StateToolTipText => "";

        public object ToolTipContent => null;

        public FontWeight FontWeight => FontWeights.Normal;

        public FontStyle FontStyle => FontStyles.Normal;

        public bool IsCut => false;

        public ImageMoniker IconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _isRoot ? KnownMonikers.Repository : Info.GetIcon(false);
            }
        }

        public ImageMoniker ExpandedIconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _isRoot ? KnownMonikers.Repository : Info.GetIcon(true);
            }
        }

        public ImageMoniker OverlayIconMoniker => KnownMonikers.Blank;

        public ImageMoniker StateIconMoniker => KnownMonikers.Blank;

        public int Priority => 0;

        public IContextMenuController ContextMenuController => null;

        public bool CanPreview => true;

        public IInvocationController InvocationController => new WorkspaceItemInvocationController();

        public int CompareTo(object obj)
        {
            return 0;
        }

        public object GetBrowseObject()
        {
            return null;
        }

        private static readonly HashSet<Type> _supportedPatterns =
        [
            typeof(ITreeDisplayItem),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(IInvocationPattern),
        ];

        private readonly bool _isRoot;

        public TPattern GetPattern<TPattern>() where TPattern : class
        {
            return _supportedPatterns.Contains(typeof(TPattern)) ? this as TPattern : null;
        }
    }
}
