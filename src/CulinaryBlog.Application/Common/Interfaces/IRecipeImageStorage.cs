namespace CulinaryBlog.Application.Common.Interfaces;

public interface IRecipeImageStorage
{
    Task<string> UploadAsync(Guid recipeId, byte[] content, string contentType, string extension, CancellationToken ct);
    Task DeleteAsync(Guid recipeId, string publicUrl, CancellationToken ct);
}
