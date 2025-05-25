using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class CvInsights
    {
        [Key]
        public int CvInsightsId { get; set; }

        public string? TechnicalSkills { get; set; }
        public string? SoftSkills { get; set; }
        public string? ToolsAndTechnologies { get; set; }
        public string? Certifications { get; set; }
        public string? ExperienceSummary { get; set; }

        public int CvId { get; set; }
        public CV Cv { get; set; } = null!;

    }
}
