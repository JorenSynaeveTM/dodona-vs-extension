using EnvDTE;
using System.Linq;

namespace dodona_vs_extension
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            // Check whether an API key is set
            var general = await General.GetLiveInstanceAsync();

            if (general == null)
            {
                _ = ShowErrorAsync("Settings could not be loaded");
                return;
            }

            var dodonaApiKey = general.DodonaApiKey;

            if (string.IsNullOrWhiteSpace(dodonaApiKey))
            {
                _ = ShowErrorAsync("Dodona API key is not set");
                return;
            }

            // TODO: Get all content from the file
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();

            // Check whether first line in code is a link to dodona

            // Post the content to Dodona

            await VS.MessageBox.ShowWarningAsync("dodona_vs_extension", "Clicked");
        }

        private async Task ShowErrorAsync(string message)
        {
            await VS.MessageBox.ShowErrorAsync("dodona_vs_extension", message);
        }
    }
}