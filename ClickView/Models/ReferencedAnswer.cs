using System.ComponentModel.DataAnnotations;

namespace ClickView.Models
{
    public class ReferencedAnswer
    {
        [Key]
        public int ReferencedAnswerId { get; set; }

        [Required]
        public string ReferencedAnswerText { get; set; }

        public int QuestionId { get; set; }

        public Question Question { get; set; }
    }
}
