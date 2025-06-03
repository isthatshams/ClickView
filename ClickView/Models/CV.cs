using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class CV
    {
        [Key]
        public int CvId { get; set; }

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public byte[] Content { get; set; } = Array.Empty<byte>();

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public CvInsights? Insights { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User? User { get; set; }

        public string? ExtractedText { get; set; } = string.Empty;
        public string? JobTitle { get; set; }

        public bool IsDefault { get; set; } = false;

        public string Template { get; set; } = "Modern"; // Default template

        public string? FilePath { get; set; }
        
        public ICollection<Interview>? Interviews { get; set; }

        public ICollection<CvEnhancement>? Enhancements { get; set; }
    }
}
