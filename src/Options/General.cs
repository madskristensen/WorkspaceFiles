using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WorkspaceFiles
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        // [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "WorkspaceFiles", "General", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    public class General : BaseOptionModel<General>, IRatingConfig
    {
        //[Category("My category")]
        //[DisplayName("My Option")]
        //[Description("An informative description.")]
        //[DefaultValue(true)]
        //public bool MyOption { get; set; } = true;

        [Browsable(false)]
        public int RatingRequests { get; set; }
    }
}
