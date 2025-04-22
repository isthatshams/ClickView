using System.ComponentModel.DataAnnotations;

namespace ClickView.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
        public bool IsActive { get; set; }
        public IEnumerable<Interview> Interviews { get; set; }
    }
}
 