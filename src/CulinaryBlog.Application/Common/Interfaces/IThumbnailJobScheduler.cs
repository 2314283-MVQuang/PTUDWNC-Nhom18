namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// FR-JOB-002: lên lịch sinh bản medium/thumbnail cho 1 ảnh công thức (Hangfire fire-and-forget),
/// gọi SAU khi ảnh đã lưu vào database.
/// </summary>
public interface IThumbnailJobScheduler
{
    void EnqueueThumbnailGeneration(Guid recipeImageId);
}
