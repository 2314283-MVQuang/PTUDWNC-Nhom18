namespace CulinaryBlog.Application.Common.Interfaces;

public interface IRecipeImageCleanup
{
    void Enqueue(Guid recipeId, string publicUrl);
}
