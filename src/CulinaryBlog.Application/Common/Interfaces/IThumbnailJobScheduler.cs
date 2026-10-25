namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// Interface lập lịch sinh thumbnail bất đồng bộ (FR-JOB-002).
/// </summary>
public interface IThumbnailJobScheduler
{
    void EnqueueThumbnailGeneration(Guid imageOrFileId);
}
