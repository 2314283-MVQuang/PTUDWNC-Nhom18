using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.Jobs;

public sealed class RecipeImageCleanupJob(IRecipeImageStorage storage)
{
    [Queue("recipe-images")]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 1800])]
    public Task DeleteAsync(Guid recipeId, string publicUrl, CancellationToken ct) => storage.DeleteAsync(recipeId, publicUrl, ct);
}

public sealed class HangfireRecipeImageCleanup(IBackgroundJobClient client) : IRecipeImageCleanup
{
    public void Enqueue(Guid recipeId, string publicUrl) =>
        client.Enqueue<RecipeImageCleanupJob>(job => job.DeleteAsync(recipeId, publicUrl, CancellationToken.None));
}
