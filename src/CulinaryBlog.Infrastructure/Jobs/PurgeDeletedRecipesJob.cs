using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class PurgeDeletedRecipesJob(
    CulinaryBlogDbContext dbContext,
    IFileStorageService fileStorageService,
    ILogger<PurgeDeletedRecipesJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var retentionCutoff = DateTimeOffset.UtcNow.AddDays(-30);

        var recipeIds = await dbContext.Recipes
            .IgnoreQueryFilters()
            .Where(recipe =>
                recipe.IsDeleted
                && recipe.DeletedAt.HasValue
                && recipe.DeletedAt.Value <= retentionCutoff)
            .Select(recipe => recipe.Id)
            .ToListAsync(cancellationToken);

        var imageUrls = await dbContext.RecipeImages
            .IgnoreQueryFilters()
            .Where(image => recipeIds.Contains(image.RecipeId))
            .Select(image => new { image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl })
            .ToListAsync(cancellationToken);

        var objectNames = imageUrls
            .SelectMany(image => new[]
            {
                FileStorageObjectNameResolver.FromPublicUrl(image.OriginalUrl, fileStorageService.BucketName),
                FileStorageObjectNameResolver.FromPublicUrl(image.MediumUrl, fileStorageService.BucketName),
                FileStorageObjectNameResolver.FromPublicUrl(image.ThumbnailUrl, fileStorageService.BucketName),
            })
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);

        foreach (var objectName in objectNames)
        {
            await fileStorageService.DeleteAsync(objectName, cancellationToken);
        }

        var purgedCount = await dbContext.Recipes
            .IgnoreQueryFilters()
            .Where(recipe =>
                recipe.IsDeleted
                && recipe.DeletedAt.HasValue
                && recipe.DeletedAt.Value <= retentionCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation(
            "Purged {RecipeCount} recipes deleted before {RetentionCutoff}.",
            purgedCount,
            retentionCutoff);
    }
}
