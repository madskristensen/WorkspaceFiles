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
        IInvocationPattern
    {
        private string _text;
        public WorkspaceItem(FileSystemInfo info, bool isRoot = false)
        {
            Info = info;
            Type = isRoot ? WorkspaceItemType.Root : info is FileInfo ? WorkspaceItemType.File : WorkspaceItemType.Folder;

            _text = Type == WorkspaceItemType.Root ? "Workspace" : Info.Name;
        }

        public WorkspaceItemType Type { get; }

        public FileSystemInfo Info { get; }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        public string ToolTipText => "";

        public string StateToolTipText => "";

        public object ToolTipContent => null;

        public FontWeight FontWeight => FontWeights.Normal;

        public FontStyle FontStyle => FontStyles.Normal;

        private bool _isCut;

        public bool IsCut
        {
            get => _isCut;
            set
            {
                if (_isCut != value)
                {
                    _isCut = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCut)));
                }
            }
        }

        public ImageMoniker IconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return Type == WorkspaceItemType.Root ? KnownMonikers.Repository : Info.GetIcon(false);
            }
        }

        public ImageMoniker ExpandedIconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return Type == WorkspaceItemType.Root ? KnownMonikers.Repository : Info.GetIcon(true);
            }
        }

        public ImageMoniker OverlayIconMoniker => default;

        public ImageMoniker StateIconMoniker => default;

        public int Priority => 0;

        public IContextMenuController ContextMenuController => new WorkspaceItemContextMenuController();

        public bool CanPreview => true;

        public IInvocationController InvocationController => new WorkspaceItemInvocationController();

        public int CompareTo(object obj)
        {
            if (obj is ITreeDisplayItem item)
            {
                // Order by caption
                return StringComparer.OrdinalIgnoreCase.Compare(Text, item.Text);
            }

            return 0;
        }

        public object GetBrowseObject() => null;

        private static readonly HashSet<Type> _supportedPatterns =
        [
            typeof(ITreeDisplayItem),
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
