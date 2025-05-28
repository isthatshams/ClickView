using System.Collections.Generic;

namespace ClickView.DTOs
{
    public class AnswerAnalysisDto
    {
        public string Tone { get; set; }
        public List<string> PersonalityTraits { get; set; }
        public List<string> SoftSkills { get; set; }
    }
} 