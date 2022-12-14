using dodona_vs_extension.Models;
using EnvDTE;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace dodona_vs_extension
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                await ValidateSettingsAsync();
                Submission submission = await ValidateFileAsync();
                //await SubmitToDodonaAsync(submission);
                await SetInfobarMessageAsync("Code has been submitted.");
                await SetInfobarMessageAsync("Awaiting results");
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync(ex.Message, "Error occured");
            }
        }

        /// <summary>
        /// Validates if the current open file has a Dodona URL in the first line
        /// </summary>
        /// <returns>
        /// a Submission object containing all information that should be submitted to Dodona
        /// </returns>
        private async Task<Submission> ValidateFileAsync()
        {
            // Get all content from the file
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            string content = docView.Document.TextBuffer.CurrentSnapshot.GetText();

            // Check whether first line in code is a link to dodona
            var lines = content.Split('\n');
            var firstLine = lines.First();
            var regex = new Regex(@".*(dodona.ugent.be).*(\/courses\/)(\d*)(\/series\/)(\d*)(\/activities\/)(\d*)");
            var match = regex.Match(firstLine);

            if (!match.Success) throw new Exception("First line of code is not a link to Dodona");

            // Get all information from the dodonaLink
            var dodonaLink = match.Groups[0].Value;
            var submission = CreateSubmissionContent(match.Groups, content);
            return submission;
        }

        private async Task ValidateSettingsAsync()
        {
            // Get the general settings
            var general = await General.GetLiveInstanceAsync();

            // If the settings could not be loaded
            if (general == null) throw new Exception("Settings could not be loaded.");

            // If the Dodona API key is not set
            if (string.IsNullOrWhiteSpace(general.DodonaApiKey))
                throw new Exception("Dodona API key is not set.");

            // If no document is open
            var activeDocument = await VS.Documents.GetActiveDocumentViewAsync();
            if (activeDocument == null) throw new Exception("No document is open. Please open a file in your editor.");
        }

        private async Task ShowErrorAsync(string message, string title = "dodona_vs_extension")
        {
            await VS.MessageBox.ShowErrorAsync(title, message);
        }

        private Submission CreateSubmissionContent(GroupCollection dodonaGroups, string code)
        {
            var courseId = dodonaGroups[3];
            var exerciseId = dodonaGroups[7];

            return new Submission()
            {
                SubmissionData = new SubmissionData()
                {
                    Code = code,
                    CourseId = int.Parse(courseId.Value),
                    ExerciseId = int.Parse(exerciseId.Value)
                }
            };
        }

        private async Task SubmitToDodonaAsync(Submission content)
        {
            // Get general settings
            var general = await General.GetLiveInstanceAsync();
            // Set a baseUrl
            string baseUrl = "https://dodona.ugent.be";
            string myContent = JsonConvert.SerializeObject(content);
            var buffer = System.Text.Encoding.UTF8.GetBytes(myContent);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var client = new HttpClient()
            {
                BaseAddress = new Uri(baseUrl)
            };
            client.DefaultRequestHeaders.Add("Authorization", $"Token token=\"{general.DodonaApiKey}\"");

            var res = await client.PostAsync("/submissions.json", byteContent);
        }

        /// <summary>
        /// Sets a message in the infobar
        /// </summary>
        /// <param name="text"></param>
        /// <returns>The infobar</returns>
        private async Task<InfoBar> SetInfobarMessageAsync(string text)
        {
            var model = new InfoBarModel(
    new[] {
        new InfoBarTextSpan(text),
        new InfoBarHyperlink("Click me")
    },
    KnownMonikers.PlayStepGroup,
    true);

            InfoBar infoBar = await VS.InfoBar.CreateAsync(ToolWindowGuids80.SolutionExplorer, model);
            infoBar.ActionItemClicked += InfoBar_ActionItemClicked;
            await infoBar.TryShowInfoBarUIAsync();
            return infoBar;
        }

        private void InfoBar_ActionItemClicked(object sender, InfoBarActionItemEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.ActionItem.Text == "Click me")
            {
                // do something
            }
        }

        private void DisimssInfobar(int seconds, InfoBar infoBar)
        {
            var timer = new System.Timers.Timer();
            timer.Interval = seconds * 1000;
            timer.Elapsed += (o, e) =>
            {
                infoBar.Close();
            };
            timer.Start();
        }
    }
}