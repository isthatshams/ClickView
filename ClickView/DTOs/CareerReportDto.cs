using System.Collections.Generic;

namespace ClickView.DTOs
{
    public class CareerReportDto
    {
        public string Strengths { get; set; }
        public string Weaknesses { get; set; }
        public List<string> Skills { get; set; }
        public List<string> JobSuggestions { get; set; }
        public List<RecommendedCourseDto> RecommendedCourses { get; set; }
    }
} 