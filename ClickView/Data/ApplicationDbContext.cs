using Microsoft.EntityFrameworkCore;
using ClickView.Models;

namespace ClickView.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Interview> Interviews { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<UserAnswer> UserAnswers { get; set; }
        public DbSet<ReferencedAnswer> ReferencedAnswers { get; set; }
        public DbSet<CV> CVs { get; set; }
        public DbSet<CvInsights> CvInsights { get; set; }
    }
}
