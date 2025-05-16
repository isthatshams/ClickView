using System.ComponentModel.DataAnnotations;

namespace ClickView.Models
{
    public class UserAnswer
    {
        [Key]
        public int UserAnswerId { get; set; }

        [Required]
        public string UserAnswerText { get; set; }

        public string? UserAnswerNotes { get; set; }

        public int QuestionId { get; set; }

        public Question Question { get; set; }

        public int InterviewId { get; set; }

        public Interview Interview { get; set; }
        public int? EvaluationScore { get; set; }
        public string? EvaluationFeedback { get; set; }
        public byte[]? AudioData { get; set; }
        public string? TranscribedText { get; set; }
    }
}
