using System.Net;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>Raw SQL full-text và cache phải được kiểm tra trên PostgreSQL/Redis thật.</summary>
public sealed class SearchPostgresFactAttribute : FactAttribute
{
    public SearchPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CULINARY_RECIPE_TEST_POSTGRES"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CULINARY_RECIPE_TEST_REDIS")))
            Skip = "Chạy test-chung-thien-y-tuan-6.ps1 để kiểm tra PostgreSQL/Redis tạm.";
    }
}

public partial class RecipeApiTests
{
    [Theory]
    [InlineData("minPrepTime=-1")]
    [InlineData("maxPrepTime=-1")]
    [InlineData("minCookTime=-1")]
    [InlineData("maxCookTime=-1")]
    [InlineData("minPrepTime=30&maxPrepTime=20")]
    [InlineData("minCookTime=60&maxCookTime=10")]
    [InlineData("difficulty=0")]
    [InlineData("difficulty=99")]
    [InlineData("categoryId=00000000-0000-0000-0000-000000000000")]
    public async Task Search_RejectsInvalidFiltersWithProblemDetails(string query)
    {
        using var response = await api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/search?q=pho&{query}");
        var problem = await AssertProblem(response, HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR");
        Assert.True(problem.GetProperty("errors").EnumerateObject().Any());
    }

    [Theory]
    [InlineData("categoryId=not-a-guid")]
    [InlineData("difficulty=Impossible")]
    [InlineData("minCookTime=1.5")]
    [InlineData("maxPrepTime=2147483648")]
    public async Task Search_RejectsMalformedParameterTypes(string query)
    {
        using var response = await api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/search?q=pho&{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SearchPostgresFact]
    public async Task Search_FiltersIndividuallyAndByIntersection_WithInclusiveBounds()
    {
        var keyword = $"search{Guid.NewGuid():N}";
        var otherCategory = new Category { Id = Guid.NewGuid(), Name = keyword, Slug = keyword };
        var recipes = new[]
        {
            SearchRecipe(keyword, api.CategoryId),
            SearchRecipe(keyword, otherCategory.Id),
            SearchRecipe(keyword, api.CategoryId, RecipeDifficulty.Hard),
            SearchRecipe(keyword, api.CategoryId, prep: 5),
            SearchRecipe(keyword, api.CategoryId, prep: 40),
            SearchRecipe(keyword, api.CategoryId, cook: 0),
            SearchRecipe(keyword, api.CategoryId, cook: 70),
            SearchRecipe(keyword, api.CategoryId, status: RecipeStatus.Draft),
            SearchRecipe(keyword, api.CategoryId, status: RecipeStatus.Archived),
            SearchRecipe(keyword, api.CategoryId),
        };
        recipes[^1].IsDeleted = true;
        await api.WithDatabaseAsync(async db =>
        {
            db.Categories.Add(otherCategory);
            db.Recipes.AddRange(recipes);
            await db.SaveChangesAsync();
        });
        var visible = recipes.Where(r => !r.IsDeleted && r.Status == RecipeStatus.Published).ToArray();
        var cases = new (string Query, Func<Recipe, bool> Matches)[]
        {
            ("", _ => true),
            ($"&categoryId={api.CategoryId}", r => r.CategoryId == api.CategoryId),
            ("&difficulty=Easy", r => r.Difficulty == RecipeDifficulty.Easy),
            ("&minPrepTime=20", r => r.PrepTime >= 20),
            ("&maxPrepTime=20", r => r.PrepTime <= 20),
            ("&minCookTime=30", r => r.CookTime >= 30),
            ("&maxCookTime=30", r => r.CookTime <= 30),
            ("&minCookTime=0&maxCookTime=0", r => r.CookTime == 0),
            ($"&categoryId={api.CategoryId}&difficulty=Easy&minPrepTime=20&maxPrepTime=20&minCookTime=30&maxCookTime=30",
                r => r.CategoryId == api.CategoryId && r.Difficulty == RecipeDifficulty.Easy && r.PrepTime == 20 && r.CookTime == 30),
            ($"&categoryId={Guid.NewGuid()}", _ => false),
        };
        foreach (var (query, matches) in cases)
        {
            // Title có dấu Phở, query không dấu pho: giữ full-text của FR-SRCH-001.
            var actual = await SearchIds($"q=pho%20{keyword}{query}");
            Assert.Equal(visible.Where(matches).Select(r => r.Id).Order(), actual.Order());
        }
        Assert.Empty(await SearchIds($"categoryId={api.CategoryId}"));
    }

    [SearchPostgresFact]
    public async Task Search_FiltersBeforeLimit_AndExcludesUnpublishedBeforeLimit()
    {
        var keyword = $"limit{Guid.NewGuid():N}";
        var target = SearchRecipe(keyword, api.CategoryId);
        target.Title = "Món phù hợp";
        target.Description = keyword; // Hạng thấp hơn những món không phù hợp có keyword trong title.
        var excluded = Enumerable.Range(0, 60)
            .Select(_ => SearchRecipe(keyword, api.CategoryId, RecipeDifficulty.Hard)).ToList();
        excluded.AddRange(Enumerable.Range(0, 60)
            .Select(_ => SearchRecipe(keyword, api.CategoryId, status: RecipeStatus.Draft)));
        await api.WithDatabaseAsync(async db =>
        {
            db.Recipes.AddRange(excluded.Append(target));
            await db.SaveChangesAsync();
        });
        var ids = await SearchIds($"q={keyword}&difficulty=Easy&maxPrepTime=20&maxCookTime=30");
        Assert.Equal(new[] { target.Id }, ids);
        Assert.Equal(50, (await SearchIds($"q={keyword}")).Length);
    }

    [SearchPostgresFact]
    public async Task Search_RedisCacheVariesByAllFilterParameters()
    {
        var keyword = $"cache{Guid.NewGuid():N}";
        var easy = SearchRecipe(keyword, api.CategoryId);
        var hard = SearchRecipe(keyword, api.CategoryId, RecipeDifficulty.Hard, prep: 40, cook: 70);
        await api.WithDatabaseAsync(async db =>
        {
            db.Recipes.AddRange(easy, hard);
            await db.SaveChangesAsync();
        });
        var query = $"q={keyword}&difficulty=Easy";
        Assert.Equal(new[] { easy.Id }, await SearchIds(query));
        await api.WithDatabaseAsync(async db =>
        {
            var stored = await db.Recipes.SingleAsync(r => r.Id == easy.Id);
            stored.IsDeleted = true; // Thay trực tiếp trong test, cố ý không evict cache.
            await db.SaveChangesAsync();
        });
        Assert.Equal(new[] { easy.Id }, await SearchIds(query)); // Chứng minh cache hit thực.
        Assert.Equal(new[] { hard.Id }, await SearchIds($"q={keyword}&difficulty=Hard"));
        foreach (var filter in new[] { "minPrepTime=40", "maxPrepTime=40", "minCookTime=70", "maxCookTime=70", $"categoryId={api.CategoryId}" })
            Assert.Equal(new[] { hard.Id }, await SearchIds($"q={keyword}&{filter}"));
        Assert.Empty(await SearchIds($"q={keyword}&maxCookTime=30"));
    }

    private async Task<Guid[]> SearchIds(string query)
    {
        var response = await api.GetJsonAsync($"/api/v1/recipes/search?{query}");
        Assert.False(response.TryGetProperty("meta", out _)); // Không triển khai phân trang của Tiến.
        return response.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToArray();
    }

    private static Recipe SearchRecipe(string keyword, Guid categoryId,
        RecipeDifficulty difficulty = RecipeDifficulty.Easy, int prep = 20, int cook = 30,
        RecipeStatus status = RecipeStatus.Published) => new()
    {
        Title = $"Phở {keyword}", Slug = $"{keyword}-{Guid.NewGuid():N}", Description = "Món ăn kiểm thử",
        CategoryId = categoryId, AuthorId = "author-1", Difficulty = difficulty,
        PrepTime = prep, CookTime = cook, Servings = 2, Status = status,
        PublishedAt = status == RecipeStatus.Published ? DateTimeOffset.UtcNow : null,
    };
}
