//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Diagnostics;
//using System.IO;
//using System.Threading;
//using System.Windows;
//using Microsoft.Internal.VisualStudio.PlatformUI;
//using Microsoft.VisualStudio.Imaging;
//using Microsoft.VisualStudio.Imaging.Interop;

//namespace WorkspaceFiles
//{
//    [DebuggerDisplay("{Text}")]
//    internal class WorkspaceItem :
//        ITreeDisplayItem,
//        ITreeDisplayItemWithImages,
//        IPrioritizedComparable,
//        IBrowsablePattern,
//        IInteractionPatternProvider,
//        IContextMenuPattern,
//        IInvocationPattern,
//        INotifyPropertyChanged,
//        ISupportDisposalNotification
//    {
//        private string _text;
//        private bool _isCut;

//        public WorkspaceItem(FileSystemInfo info, bool isRoot = false)
//        {
//            Info = info;
//            Type = isRoot ? WorkspaceItemType.Root : info is FileInfo ? WorkspaceItemType.File : WorkspaceItemType.Folder;

//            _text = Type == WorkspaceItemType.Root ? "File Explorer" : Info.Name;
//        }

//        public WorkspaceItemType Type { get; }

//        public FileSystemInfo Info { get; set; }

//        public string Text
//        {
//            get => _text;
//            set
//            {
//                if (_text != value)
//                {
//                    _text = value;
//                    RaisePropertyChanged(nameof(Text));
//                }
//            }
//        }

//        public string ToolTipText => "";

//        public string StateToolTipText => "";

//        public object ToolTipContent => null;

//        public FontWeight FontWeight => FontWeights.Normal;

//        public FontStyle FontStyle => FontStyles.Normal;

//        public bool IsCut
//        {
//            get => _isCut;
//            set
//            {
//                if (_isCut != value)
//                {
//                    _isCut = value;
//                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCut)));
//                }
//            }
//        }

//        public ImageMoniker IconMoniker
//        {
//            get
//            {
//                ThreadHelper.ThrowIfNotOnUIThread();
//                return Type == WorkspaceItemType.Root ? KnownMonikers.RemoteFolder : Info.GetIcon(false);
//            }
//        }

//        public ImageMoniker ExpandedIconMoniker
//        {
//            get
//            {
//                ThreadHelper.ThrowIfNotOnUIThread();
//                return Type == WorkspaceItemType.Root ? KnownMonikers.RemoteFolderOpen : Info.GetIcon(true);
//            }
//        }

//        public ImageMoniker OverlayIconMoniker => default;

//        public ImageMoniker StateIconMoniker => default;

//        public int Priority => 0;

//        public IContextMenuController ContextMenuController => new WorkspaceItemContextMenuController();

//        public bool CanPreview => true;

//        public IInvocationController InvocationController => new WorkspaceItemInvocationController();

//        public bool IsDisposed { get; private set; }

//        public void Dispose()
//        {
//            if (!IsDisposed)
//            {
//                IsDisposed = true;
//                RaisePropertyChanged(nameof(IsDisposed));
//            }
//        }

//        public int CompareTo(object obj)
//        {
//            if (obj is ITreeDisplayItem item)
//            {
//                // Order by caption
//                return StringComparer.OrdinalIgnoreCase.Compare(Text, item.Text);
//            }

//            return 0;
//        }

//        public object GetBrowseObject() => null;

//        private static readonly HashSet<Type> _supportedPatterns =
//        [
//            typeof(ITreeDisplayItem),
//            typeof(IBrowsablePattern),
//            typeof(IContextMenuPattern),
//            typeof(IInvocationPattern),
//            typeof(ISupportExpansionEvents),
//            typeof(ISupportDisposalNotification),
//        ];

//        public event PropertyChangedEventHandler PropertyChanged;

//        public void RaisePropertyChanged(string propertyName)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        }

//        public TPattern GetPattern<TPattern>() where TPattern : class
//        {
//            if (!IsDisposed)
//            {
//                if (_supportedPatterns.Contains(typeof(TPattern)))
//                {
//                    return this as TPattern;
//                }
//            }
//            else
//            {
//                // If this item has been deleted, it no longer supports any patterns
//                // other than ISupportDisposalNotification.
//                // It's valid to use GetPattern on a deleted item, but there are no
//                // longer any pattern contracts it fulfills other than the contract
//                // that reports the item as a dead ITransientObject.
//                if (typeof(TPattern) == typeof(ISupportDisposalNotification))
//                {
//                    return this as TPattern;
//                }
//            }

//            return null;
//        }
//    }
//}
