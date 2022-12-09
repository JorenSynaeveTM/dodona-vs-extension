using System.ComponentModel;
using System.Runtime.InteropServices;

namespace dodona_vs_extension
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        // [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "dodona_vs_extension", "General", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General>
        { }
    }

    public class General : BaseOptionModel<General>
    {
        [Category("My category")]
        [DisplayName("My Option")]
        [Description("An informative description.")]
        [DefaultValue(true)]
        public bool MyOption { get; set; } = true;

        [Category("Authorization")]
        [DisplayName("Dodona API key")]
        [Description("Specifies which API token should be used to authorize to Dodona.")]
        [DefaultValue("")]
        public string DodonaApiKey { get; set; } = "";
    }
}