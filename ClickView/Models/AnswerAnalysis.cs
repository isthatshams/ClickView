using System.ComponentModel.DataAnnotations;

namespace ClickView.Models
{
    public class AnswerAnalysis
    {
        [Key]
        public int AnswerAnalysisId { get; set; }
        public string Tone { get; set; }
        public string PersonalityTraits { get; set; } // JSON array as string
        public string SoftSkills { get; set; }        // JSON array as string
        public int UserAnswerId { get; set; }
        public UserAnswer UserAnswer { get; set; }
    }
} 