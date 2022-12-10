using dodona_vs_extension.Models;
using EnvDTE;
using Newtonsoft.Json;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace dodona_vs_extension
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
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

                // Get all content from the file
                DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
                string content = docView.Document.TextBuffer.CurrentSnapshot.GetText();

                // Check whether first line in code is a link to dodona
                var lines = content.Split('\n');
                var firstLine = lines.First();
                var regex = new Regex(@".*(dodona.ugent.be).*(\/courses\/)(\d*)(\/series\/)(\d*)(\/activities\/)(\d*)");
                var match = regex.Match(firstLine);

                if (!match.Success)
                {
                    _ = ShowErrorAsync("First line of code is not a link to Dodona");
                    return;
                }

                // Get all information from the dodonaLink
                var dodonaLink = match.Groups[0].Value;
                var submission = CreateSubmissionContent(match.Groups, content);

                // Create a submission on Dodona
                await PostToDodonaAsync(submission, dodonaApiKey);

                await VS.MessageBox.ShowWarningAsync("dodona_vs_extension", "Clicked");
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync(ex.Message, "Error occured");
            }
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

        private async Task PostToDodonaAsync(Submission content, string dodonaApiKey)
        {
            string baseUrl = "https://dodona.ugent.be";
            string myContent = JsonConvert.SerializeObject(content);
            var buffer = System.Text.Encoding.UTF8.GetBytes(myContent);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var client = new HttpClient()
            {
                BaseAddress = new Uri(baseUrl)
            };
            client.DefaultRequestHeaders.Add("Authorization", $"Token token=\"{dodonaApiKey}\"");

            var res = await client.PostAsync("/submissions.json", byteContent);
        }
    }
}