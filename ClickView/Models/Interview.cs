using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class Interview
    {
        [Key]
        public int InterviewId { get; set; }
        public InterviewType InterviewType { get; set; } // Chat or Voice
        public double InterviewMark { get; set; }

        //User Connection
        [ForeignKey(nameof(UserId))]
        public int UserId { get; set; } //Foreign Key
        public User? User { get; set; }

        //Questions Connection
        public IEnumerable<Question> Questions { get; set; }

        //UserAnswer Connection
        public IEnumerable<UserAnswer> UserAnswers { get; set; }

        //RefrencedAnswer Connection
        public IEnumerable<RefrencedAnswer> RefrencedAnswer { get; set; }
    }
}
