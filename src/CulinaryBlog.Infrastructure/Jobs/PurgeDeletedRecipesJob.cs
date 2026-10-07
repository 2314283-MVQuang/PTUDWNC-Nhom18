using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class PurgeDeletedRecipesJob(
    CulinaryBlogDbContext dbContext,
    ILogger<PurgeDeletedRecipesJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var retentionCutoff = DateTimeOffset.UtcNow.AddDays(-30);
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
