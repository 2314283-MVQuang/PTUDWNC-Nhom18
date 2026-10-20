using System.Net;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-SRCH-003 (Thịnh) — sắp xếp kết quả /recipes/search qua API.</summary>
public partial class RecipeApiTests
{
    [Theory]
    [InlineData("authorId")]
    [InlineData("-password")]
    [InlineData("title;DROP")]
    public async Task Search_RejectsSortOutsideWhitelist_With400(string sort)
    {
        using var response = await api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/search?q=pho&sort={Uri.EscapeDataString(sort)}");
        await AssertProblem(response, HttpStatusCode.BadRequest, "INVALID_SORT");
    }

    [SearchPostgresFact]
    public async Task Search_SortsInSqlBeforeLimit()
    {
        var keyword = $"sort{Guid.NewGuid():N}";
        // 60 món có keyword trong tiêu đề (độ liên quan cao) và 1 món chỉ có keyword trong mô tả
        // (độ liên quan thấp nhất) nhưng nấu nhanh nhất. Nếu sắp xếp SAU khi cắt 50 dòng theo độ
        // liên quan thì món này bị loại; sắp xếp trong SQL thì nó phải đứng đầu.
        var popular = Enumerable.Range(0, 60)
            .Select(i => SearchRecipe(keyword, api.CategoryId, cook: 30 + i)).ToList();
        var quickest = SearchRecipe(keyword, api.CategoryId, cook: 1);
        quickest.Title = "Món nấu nhanh";
        quickest.Description = keyword;
        await api.WithDatabaseAsync(async db =>
        {
            db.Recipes.AddRange(popular.Append(quickest));
            await db.SaveChangesAsync();
        });

        var ascending = await SearchIds($"q={keyword}&sort=cookTime");
        Assert.Equal(quickest.Id, ascending[0]);

        var descending = await SearchIds($"q={keyword}&sort=-cookTime");
        Assert.Equal(popular.OrderByDescending(r => r.CookTime).First().Id, descending[0]);
        Assert.DoesNotContain(quickest.Id, descending); // món nấu lâu nhất trước, giới hạn 50 dòng

        var byRelevance = await SearchIds($"q={keyword}");
        Assert.DoesNotContain(quickest.Id, byRelevance);
    }
}
