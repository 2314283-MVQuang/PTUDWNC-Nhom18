using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>FR-FILE-002: mỗi object một job riêng để object lỗi không chặn các object còn lại.</summary>
public sealed class HangfireFileDeletionScheduler(IBackgroundJobClient jobs) : IFileDeletionScheduler
{
    public int Enqueue(IEnumerable<string> objectNames)
    {
        var count = 0;
        foreach (var objectName in objectNames.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            jobs.Enqueue<DeleteFileJob>(job => job.ExecuteAsync(objectName, CancellationToken.None));
            count++;
        }

        return count;
    }
}
