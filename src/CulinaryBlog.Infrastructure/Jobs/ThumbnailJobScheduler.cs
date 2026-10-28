using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-JOB-002: enqueue <see cref="GenerateThumbnailJob"/>. SRS: thumbnail lỗi thì ảnh gốc vẫn hiển
/// thị — nên nếu không enqueue được (Hangfire storage tạm lỗi) chỉ ghi log, KHÔNG làm request
/// upload ảnh (đã lưu xong) trả 500.
/// </summary>
public class ThumbnailJobScheduler(IBackgroundJobClient backgroundJobs, ILogger<ThumbnailJobScheduler> logger)
    : IThumbnailJobScheduler
{
    public void EnqueueThumbnailGeneration(Guid recipeImageId)
    {
        try
        {
            backgroundJobs.Enqueue<GenerateThumbnailJob>(job => job.ExecuteAsync(recipeImageId, CancellationToken.None));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không enqueue được GenerateThumbnailJob cho ảnh {ImageId}; ảnh gốc vẫn hiển thị.", recipeImageId);
        }
    }
}
