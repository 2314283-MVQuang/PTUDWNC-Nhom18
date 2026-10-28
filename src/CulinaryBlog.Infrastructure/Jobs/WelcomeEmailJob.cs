using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class WelcomeEmailJob(
    CulinaryBlogDbContext db,
    IWelcomeEmailSender sender,
    ILogger<WelcomeEmailJob> logger)
{
    // FR-JOB-001, SRS trang 38: thử lại sau 1 phút, 5 phút và 30 phút.
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 1800 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(string userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            logger.LogWarning("WelcomeEmailJob skipped: user {UserId} is missing, inactive or has no email.", userId);
            return;
        }

        await sender.SendAsync(user.Email, user.DisplayName, ct);
        logger.LogInformation("WelcomeEmailJob completed for user {UserId}.", userId);
    }
}
