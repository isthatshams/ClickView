using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class FeedbackReport
    {
        [Key]
        public int FeedbackReportId { get; set; }
        public string Strengths { get; set; }
        public string Weaknesses { get; set; }
        public string PersonalitySummary { get; set; }
        public string Recommendation { get; set; }
        [ForeignKey(nameof(Interview))]
        public int InterviewId { get; set; }
        public Interview Interview { get; set; }
    }
} 