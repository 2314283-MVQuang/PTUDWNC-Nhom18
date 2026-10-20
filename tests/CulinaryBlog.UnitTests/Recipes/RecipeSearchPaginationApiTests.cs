using System.Net;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-SRCH-004 (Tiến) — phân trang /recipes/search qua API.</summary>
public partial class RecipeApiTests
{
    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    public async Task Search_RejectsInvalidPaging_With422(string query)
    {
        using var response = await api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/search?q=pho&{query}");
        await AssertProblem(response, HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR");
    }

    [SearchPostgresFact]
    public async Task Search_PaginatesAllFilteredResults_WithMeta()
    {
        var keyword = $"page{Guid.NewGuid():N}";
        var recipes = Enumerable.Range(0, 25)
            .Select(i => SearchRecipe(keyword, api.CategoryId, cook: 100 + i)).ToList();
        await api.WithDatabaseAsync(async db =>
        {
            db.Recipes.AddRange(recipes);
            await db.SaveChangesAsync();
        });

        var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var json = await api.GetJsonAsync($"/api/v1/recipes/search?q={keyword}&sort=cookTime&page={page}&pageSize=10");
            var meta = json.GetProperty("meta");
            Assert.Equal(page, meta.GetProperty("page").GetInt32());
            Assert.Equal(10, meta.GetProperty("pageSize").GetInt32());
            Assert.Equal(25, meta.GetProperty("total").GetInt32());
            Assert.Equal(3, meta.GetProperty("totalPages").GetInt32());
            Assert.Equal(page < 3, meta.GetProperty("hasNextPage").GetBoolean());
            Assert.Equal(page > 1, meta.GetProperty("hasPreviousPage").GetBoolean());
            seen.AddRange(json.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()));
        }

        // 3 trang ghép lại đúng 25 món, không trùng, đúng thứ tự cookTime tăng dần.
        Assert.Equal(recipes.OrderBy(r => r.CookTime).Select(r => r.Id), seen);

        // Mặc định pageSize = 12.
        var firstPage = await api.GetJsonAsync($"/api/v1/recipes/search?q={keyword}");
        Assert.Equal(12, firstPage.GetProperty("data").GetArrayLength());
    }
}
