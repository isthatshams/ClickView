using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class Question
    {
        [Key]
        public int QuestionId { get; set; }
        public required string QuestionText { get; set; }
        public DifficultyLevel DifficultyLevel { get; set; }
        public double QuestionMark{ get; set; }

        //Interview Connection
        [ForeignKey(nameof(InterviewId))]
        public int InterviewId { get; set; }
        public Interview? Interview { get; set; }

        //UserAnswers Connection
        [ForeignKey(nameof(UserAnswerId))]
        public int UserAnswerId { get; set; }
        public RefrencedAnswer? UserAnswer { get; set; }

        //RefrencedAnswer Connection
        [ForeignKey(nameof(RefrencedAnswerId))]
        public int RefrencedAnswerId { get; set; }
        public RefrencedAnswer? RefrencedAnswer { get; set; }
    }
}
