using System.ComponentModel.DataAnnotations;

namespace ClickView.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationCode { get; set; }
        public DateTime? VerificationCodeExpiry { get; set; }
        public DateTime? LastVerificationEmailSentAt { get; set; }

        [Required]
        public string PasswordHash { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public bool IsActive { get; set; }

        public List<Interview> Interviews { get; set; } = new();
        public ICollection<CV> CVs { get; set; } //Multiple CVs
    }
}
