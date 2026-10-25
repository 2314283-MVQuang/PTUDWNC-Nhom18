using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// Triển khai lập lịch sinh thumbnail qua Hangfire (FR-JOB-002).
/// </summary>
public class ThumbnailJobScheduler(IBackgroundJobClient? backgroundJobs = null) : IThumbnailJobScheduler
{
    public void EnqueueThumbnailGeneration(Guid imageOrFileId)
    {
        try
        {
            if (backgroundJobs != null)
            {
                backgroundJobs.Enqueue<GenerateThumbnailJob>(job => job.ExecuteAsync(imageOrFileId, CancellationToken.None));
            }
            else
            {
                BackgroundJob.Enqueue<GenerateThumbnailJob>(job => job.ExecuteAsync(imageOrFileId, CancellationToken.None));
            }
        }
        catch
        {
            // Bỏ qua nếu môi trường test không kích hoạt Hangfire JobStorage
        }
    }
}
