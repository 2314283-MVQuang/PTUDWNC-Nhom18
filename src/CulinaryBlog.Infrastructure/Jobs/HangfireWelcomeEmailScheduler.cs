using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class HangfireWelcomeEmailScheduler(IBackgroundJobClient jobs) : IWelcomeEmailScheduler
{
    public string Enqueue(string userId) => jobs.Enqueue<WelcomeEmailJob>(
        job => job.ExecuteAsync(userId, CancellationToken.None));
}
