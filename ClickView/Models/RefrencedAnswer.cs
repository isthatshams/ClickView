using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class RefrencedAnswer
    {
        [Key]
        public int RefrencedAnswerId { get; set; }
        public String RefrencedAnswerText { get; set; }

        //Question Connection
        [ForeignKey(nameof(QuestionId))]
        public int QuestionId { get; set; }
        public Question? Question { get; set; }

        //UserAnswer Connection
        [ForeignKey(nameof(UserAnswerId))]
        public int UserAnswerId { get; set; }
        public RefrencedAnswer? UserAnswer { get; set; }

        //Interview Connection
        [ForeignKey(nameof(InterviewId))]
        public int InterviewId { get; set; }
        public Interview? Interview { get; set; }
    }
}
