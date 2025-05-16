using System.ComponentModel.DataAnnotations;

namespace ClickView.Models
{
    public class Question
    {
        [Key]
        public int QuestionId { get; set; }

        [Required]
        public string QuestionText { get; set; }

        public DifficultyLevel DifficultyLevel { get; set; }

        public double QuestionMark { get; set; }

        public int InterviewId { get; set; }

        public Interview Interview { get; set; }

        public ReferencedAnswer ReferencedAnswer { get; set; }

        public List<UserAnswer> UserAnswers { get; set; } = new();
    }
}
