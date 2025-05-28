using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class RecommendedCourse
    {
        [Key]
        public int RecommendedCourseId { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        [ForeignKey(nameof(CareerReport))]
        public int CareerReportId { get; set; }
        public CareerReport CareerReport { get; set; }
    }
} 