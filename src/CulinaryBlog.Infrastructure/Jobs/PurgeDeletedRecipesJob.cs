using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-JOB-004 (mâu thuẫn #1): xoá cứng công thức đã xoá mềm quá 30 ngày.
/// FR-FILE-002: ảnh của các công thức đó (bản gốc, medium, thumbnail) được lên lịch xoá khỏi MinIO
/// SAU khi xoá DB — MinIO tạm lỗi thì từng file tự retry, không chặn việc dọn dữ liệu.
/// </summary>
public sealed class PurgeDeletedRecipesJob(
    CulinaryBlogDbContext dbContext,
    IFileStorageService fileStorageService,
    IFileDeletionScheduler fileDeletionScheduler,
    ILogger<PurgeDeletedRecipesJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var retentionCutoff = DateTimeOffset.UtcNow.AddDays(-30);

        var expiredRecipes = dbContext.Recipes
            .IgnoreQueryFilters()
            .Where(recipe =>
                recipe.IsDeleted
                && recipe.DeletedAt.HasValue
                && recipe.DeletedAt.Value <= retentionCutoff);

        // Lấy URL ảnh trước, vì ExecuteDeleteAsync cascade xoá luôn bảng RecipeImages.
        var imageUrls = await dbContext.RecipeImages
            .IgnoreQueryFilters()
            .Where(image => expiredRecipes.Select(recipe => recipe.Id).Contains(image.RecipeId))
            .Select(image => new { image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl })
            .ToListAsync(cancellationToken);

        var objectNames = imageUrls
            .SelectMany(image => FileStorageObjectNameResolver.FromImageUrls(
                fileStorageService.BucketName, image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var purgedCount = await expiredRecipes.ExecuteDeleteAsync(cancellationToken);
        var scheduledFiles = fileDeletionScheduler.Enqueue(objectNames);

        logger.LogInformation(
            "Purged {RecipeCount} recipes deleted before {RetentionCutoff}; scheduled {FileCount} MinIO objects for deletion.",
            purgedCount,
            retentionCutoff,
            scheduledFiles);
    }
}
