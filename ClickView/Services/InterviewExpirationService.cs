using Microsoft.EntityFrameworkCore;
using ClickView.Data;
using ClickView.Models;

namespace ClickView.Services
{
    public class InterviewExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InterviewExpirationService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute
        private readonly TimeSpan _interviewDuration = TimeSpan.FromMinutes(45); // 45 minutes

        public InterviewExpirationService(
            IServiceProvider serviceProvider,
            ILogger<InterviewExpirationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Interview Expiration Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndExpireInterviews();
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking interview expiration");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait longer on error
                }
            }
        }

        private async Task CheckAndExpireInterviews()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                var cutoffTime = DateTime.Now.Subtract(_interviewDuration);
                
                // Find interviews that started more than 45 minutes ago and are not finished
                var expiredInterviews = await dbContext.Interviews
                    .Where(i => i.StartedAt <= cutoffTime && !i.IsFinished)
                    .ToListAsync();

                if (expiredInterviews.Any())
                {
                    _logger.LogInformation($"Found {expiredInterviews.Count} expired interviews to mark as finished");

                    foreach (var interview in expiredInterviews)
                    {
                        interview.IsFinished = true;
                        interview.FinishedAt = DateTime.Now;
                        _logger.LogInformation($"Marked interview {interview.InterviewId} as finished (expired after 45 minutes)");
                    }

                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation($"Successfully updated {expiredInterviews.Count} expired interviews");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing expired interviews");
                throw;
            }
        }
    }
} 