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
        IRenamePattern
    {
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
            get
            {
                return _text;
            }
            set
            {
                _text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }


        public string ToolTipText => "";

        public string StateToolTipText => "";

        public object ToolTipContent => null;

        public FontWeight FontWeight => FontWeights.Normal;

        public FontStyle FontStyle => FontStyles.Normal;

        private bool _isCut;
        private string _text;

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

        public ImageMoniker OverlayIconMoniker => KnownMonikers.Blank;

        public ImageMoniker StateIconMoniker => KnownMonikers.Blank;

        public int Priority => 0;

        public IContextMenuController ContextMenuController => new WorkspaceItemContextMenuController();

        public bool CanPreview => true;

        public IInvocationController InvocationController => new WorkspaceItemInvocationController();

        public bool CanRename => Type != WorkspaceItemType.Root;

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
            typeof(IRenamePattern),
        ];

        public event PropertyChangedEventHandler PropertyChanged;

        public TPattern GetPattern<TPattern>() where TPattern : class
        {
            return _supportedPatterns.Contains(typeof(TPattern)) ? this as TPattern : null;
        }

        public IRenameItemTransaction BeginRename(object container, Func<IRenameItemTransaction, IRenameItemValidationResult> validator)
        {
            return new RenameTransaction(this, container, validator);
        }

        private class RenameTransaction : RenameItemTransaction
        {
            public RenameTransaction(WorkspaceItem namingRule, object container, Func<IRenameItemTransaction, IRenameItemValidationResult> validator)
                : base(namingRule, container, validator)
            {
                RenameLabel = namingRule.Text;
                Completed += (s, e) =>
                {
                    namingRule.Text = RenameLabel;
                };
            }

            public override void Commit(RenameItemCompletionFocusBehavior completionFocusBehavior)
            {
                base.Commit(completionFocusBehavior);
            }
        }
    }
}
