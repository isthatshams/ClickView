using System.Collections.Generic;

namespace ClickView.DTOs
{
    public class InterviewSummaryDto
    {
        public string OverallTone { get; set; }
        public List<string> DominantPersonalityTraits { get; set; }
        public List<string> DominantSoftSkills { get; set; }
        public List<string> Strengths { get; set; }
        public List<string> Weaknesses { get; set; }
        public List<string> SuggestedImprovements { get; set; }
    }
} 