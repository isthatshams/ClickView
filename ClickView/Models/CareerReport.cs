using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class CareerReport
    {
        [Key]
        public int CareerReportId { get; set; }
        [ForeignKey(nameof(Interview))]
        public int InterviewId { get; set; }
        public Interview Interview { get; set; }
        public string Strengths { get; set; }
        public string Weaknesses { get; set; }
        public string SkillsJson { get; set; } // JSON array of strings
        public string JobSuggestionsJson { get; set; } // JSON array of strings
        public ICollection<RecommendedCourse> RecommendedCourses { get; set; }
    }
} 