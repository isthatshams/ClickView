using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography.X509Certificates;

namespace ClickView.Models
{
    public class UserAnswer
    {
        [Key]
        public int UserAnswerId { get; set; }
        public String UserAnswerText { get; set; }
        public string UserAnswerNotes { get; set; }

        //Question Connection
        [ForeignKey(nameof(QuestionId))]
        public int QuestionId { get; set; }
        public Question? Question { get; set; }

        //RefrencedAnswer Connection
        [ForeignKey(nameof(RefrencedAnswerId))]
        public int RefrencedAnswerId { get; set; }
        public RefrencedAnswer? RefrencedAnswer { get; set; }

        //Interview Connection
        [ForeignKey(nameof(InterviewId))]
        public int InterviewId { get; set; }
        public Interview? Interview { get; set; }
    }
}
