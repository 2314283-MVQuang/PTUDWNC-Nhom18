using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Common.Interfaces;

public interface IRecipeRepository
{
    Task<IEnumerable<Recipe>> SearchRecipesAsync(string keyword, CancellationToken cancellationToken = default);
    Task<IEnumerable<Recipe>> GetAllAsync();
}