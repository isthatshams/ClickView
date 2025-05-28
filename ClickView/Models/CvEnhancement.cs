using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class CvEnhancement
    {
        [Key]
        public int CvEnhancementId { get; set; }
        [ForeignKey(nameof(CV))]
        public int CvId { get; set; }
        public CV CV { get; set; }
        public string JobTitle { get; set; }
        public string Suggestions { get; set; }
        public string EnhancedCvText { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
} 