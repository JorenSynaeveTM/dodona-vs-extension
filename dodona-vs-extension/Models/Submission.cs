using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dodona_vs_extension.Models
{
    public class Submission
    {
        [JsonProperty("submission")]
        public SubmissionData SubmissionData { get; set; }
    }

    public class SubmissionData
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("course_id")]
        public int CourseId { get; set; }

        [JsonProperty("exercise_id")]
        public int ExerciseId { get; set; }
    }
}