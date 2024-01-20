using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

namespace WorkspaceFiles
{
    internal class WorkspaceItem :
        ITreeDisplayItem,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IBrowsablePattern,
        IInteractionPatternProvider,
        IContextMenuPattern,
        IInvocationPattern,
        INotifyPropertyChanged
    {
        public WorkspaceItem(FileSystemInfo info, bool isRoot = false)
        {
            Info = info;
            IsRoot = isRoot;
        }

        public bool IsRoot { get; }

        public FileSystemInfo Info { get; }

        public string Text => IsRoot ? "Workspace" : Info.Name;

        public string ToolTipText => "";

        public string StateToolTipText => "";

        public object ToolTipContent => null;

        public FontWeight FontWeight => FontWeights.Normal;

        public FontStyle FontStyle => FontStyles.Normal;

        private bool _isCut;
        public bool IsCut
        {
            get { return _isCut; }
            set
            {
                _isCut = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCut)));
            }
        }

        public ImageMoniker IconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return IsRoot ? KnownMonikers.Repository : Info.GetIcon(false);
            }
        }

        public ImageMoniker ExpandedIconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return IsRoot ? KnownMonikers.Repository : Info.GetIcon(true);
            }
        }

        public ImageMoniker OverlayIconMoniker => KnownMonikers.Blank;

        public ImageMoniker StateIconMoniker => KnownMonikers.Blank;

        public int Priority => 0;

        public IContextMenuController ContextMenuController => new WorkspaceItemContextMenuController();

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
            typeof(ITreeDisplayItemWithImages),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(IInvocationPattern),
        ];

        public event PropertyChangedEventHandler PropertyChanged;

        public TPattern GetPattern<TPattern>() where TPattern : class
        {
            return _supportedPatterns.Contains(typeof(TPattern)) ? this as TPattern : null;
        }
    }
}
