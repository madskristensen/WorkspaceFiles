using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WorkspaceFiles
{
    internal partial class OptionsProvider
    {
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    public class General : BaseOptionModel<General>, IRatingConfig
    {
        [Category("General")]
        [DisplayName("Show workspace node")]
        [Description("Determines if the Workspace node should be visible in Solution Explorer")]
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        [Browsable(false)]
        public int RatingRequests { get; set; }
    }
}
