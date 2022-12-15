using dodona_vs_extension.Constants;
using dodona_vs_extension.Models;
using EnvDTE;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.VisualStudio.Threading.AsyncReaderWriterLock;
using OutputWindowPane = Community.VisualStudio.Toolkit.OutputWindowPane;

namespace dodona_vs_extension
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        private OutputWindowPane _dodonaOutputPane = null;
        private string _exerciseUrl = string.Empty;

        /// <summary>
        /// Main method when the button in the menu is clicked
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                await ValidateSettingsAsync();
                Submission submission = await ValidateFileAsync();
                ExerciseInformation exerciseInformation = await GetExerciseInformationAsync();
                var submissionResponse = await SubmitToDodonaAsync(submission);
                await SetOutputMessageAsync($"Code for \"{exerciseInformation.Name}\" has been submitted.");
                await CheckSubmissionResultAsync(submissionResponse);
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync(ex.Message, "Error occured");
            }
        }

        private async Task SetOutputMessageAsync(string v)
        {
            if (_dodonaOutputPane == null)
                _dodonaOutputPane = await VS.Windows.CreateOutputWindowPaneAsync("Dodona");

            await _dodonaOutputPane.WriteLineAsync(v);
        }

        private async Task CheckSubmissionResultAsync(SubmissionSubmittedResponse submissionResponse)
        {
            SetOutputMessageAsync("Checking submission result...");
            await VS.StatusBar.ShowMessageAsync("Checking submission result...");
            var general = await General.GetLiveInstanceAsync();
            var client = new HttpClient()
            {
                BaseAddress = new Uri("https://dodona.ugent.be")
            };
            client.DefaultRequestHeaders.Add("Authorization", $"Token token=\"{general.DodonaApiKey}\"");

            var submissionResult = new SubmissionResult() { Status = SubmissionStatus.StatusRunning };
            var timer = new System.Timers.Timer();
            timer.Elapsed += async (s, e) =>
            {
                var res = await client.GetAsync(submissionResponse.Url);
                var jsonString = await res.Content.ReadAsStringAsync();
                var submissionResult = JsonConvert.DeserializeObject<SubmissionResult>(jsonString);

                if (submissionResult.Status != SubmissionStatus.StatusRunning && submissionResult.Status != SubmissionStatus.StatusQueued)
                {
                    await VS.StatusBar.ClearAsync();
                    timer.Stop();
                    timer.Dispose();
                    SetOutputMessageAsync("Submission result: " + submissionResult.Status);
                }
                else
                {
                    SetOutputMessageAsync("Checking submission result...");
                }
            };
            timer.Interval = 5000;
            timer.Start();
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
            var regex = new Regex(@"(https:\/\/)(dodona.ugent.be).*(\/courses\/)(\d*)(\/series\/)(\d*)(\/activities\/)(\d*)");
            var match = regex.Match(firstLine);

            if (!match.Success) throw new Exception("First line of code is either not a link to Dodona or an invalid link.");

            // Get all information from the dodonaLink
            _exerciseUrl = match.Value;
            var submission = CreateSubmissionContent(match.Groups, content);
            return submission;
        }

        private async Task<ExerciseInformation> GetExerciseInformationAsync()
        {
            var general = await General.GetLiveInstanceAsync();
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Token token=\"{general.DodonaApiKey}\"");

            var res = await client.GetAsync(_exerciseUrl + ".json");
            var jsonString = await res.Content.ReadAsStringAsync();
            ExerciseInformation exerciseInformation = JsonConvert.DeserializeObject<ExerciseInformation>(jsonString);
            return exerciseInformation;
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
            var courseId = dodonaGroups[4];
            var exerciseId = dodonaGroups[8];

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

        private async Task<SubmissionSubmittedResponse> SubmitToDodonaAsync(Submission content)
        {
            // Get general settings
            var general = await General.GetLiveInstanceAsync();
            // Set a baseUrl
            string myContent = JsonConvert.SerializeObject(content);
            var buffer = System.Text.Encoding.UTF8.GetBytes(myContent);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var client = new HttpClient()
            {
                BaseAddress = new Uri("https://dodona.ugent.be")
            };
            client.DefaultRequestHeaders.Add("Authorization", $"Token token=\"{general.DodonaApiKey}\"");

            var res = await client.PostAsync("/submissions.json", byteContent);
            string responseContent = await res.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<SubmissionSubmittedResponse>(responseContent);
        }
    }
}