using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickView.Models
{
    public class CV
    {
        [Key]
        public int CvId { get; set; }

        [Required]
        public string FileName { get; set; }

        [Required]
        public string ContentType { get; set; }

        [Required]
        public byte[] Content { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public CvInsights? Insights { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User? User { get; set; }

        public string? ExtractedText { get; set; }

        public ICollection<Interview>? Interviews { get; set; }
    }
}
