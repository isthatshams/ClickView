using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class Interview
    {
        [Key]
        public int InterviewId { get; set; }

        public InterviewType InterviewType { get; set; }

        public double InterviewMark { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public int? OriginalInterviewId { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        public User User { get; set; }

        [ForeignKey(nameof(CV))]
        public int? CvId { get; set; } //Optional link to CV used
        public CV? CV { get; set; }

        public List<Question> Questions { get; set; } = new();

        public List<UserAnswer> UserAnswers { get; set; } = new();
    }
}
