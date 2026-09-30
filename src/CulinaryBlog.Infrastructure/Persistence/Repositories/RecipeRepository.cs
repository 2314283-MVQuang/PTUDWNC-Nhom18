using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public class RecipeRepository(CulinaryBlogDbContext dbContext)
    : RepositoryBase<Recipe>(dbContext), IRecipeRepository
{
    public async Task<IEnumerable<Recipe>> SearchRecipesAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var normalizedKeyword = keyword.Trim();
        var recipes =
            from recipe in DbContext.Recipes.AsNoTracking()
            let searchVector = EF.Functions.ToTsVector("simple", EF.Functions.Unaccent(recipe.Title))
            let searchQuery = EF.Functions.WebSearchToTsQuery(
                "simple",
                EF.Functions.Unaccent(normalizedKeyword))
            where searchVector.Matches(searchQuery)
            orderby searchVector.Rank(searchQuery) descending
            select recipe;

        return await recipes.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Recipe>> GetAllAsync() =>
        await DbContext.Recipes.AsNoTracking().ToListAsync();
}