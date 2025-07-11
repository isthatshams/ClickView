﻿using Microsoft.EntityFrameworkCore;
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
        public DbSet<AnswerAnalysis> AnswerAnalyses { get; set; }
        public DbSet<FeedbackReport> FeedbackReports { get; set; }
        public DbSet<CareerReport> CareerReports { get; set; }
        public DbSet<RecommendedCourse> RecommendedCourses { get; set; }
        public DbSet<CvEnhancement> CvEnhancements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure one-to-one relationship between AnswerAnalysis and UserAnswer
            modelBuilder.Entity<AnswerAnalysis>()
                .HasOne(a => a.UserAnswer)
                .WithOne(u => u.AnswerAnalysis)
                .HasForeignKey<AnswerAnalysis>(a => a.UserAnswerId);

            // Configure optional relationship between Interview and CV
            modelBuilder.Entity<Interview>()
                .HasOne(i => i.CV)
                .WithMany(cv => cv.Interviews)
                .HasForeignKey(i => i.CvId)
                .OnDelete(DeleteBehavior.SetNull); // When a CV is deleted, set CvId to null
        }
    }
}
