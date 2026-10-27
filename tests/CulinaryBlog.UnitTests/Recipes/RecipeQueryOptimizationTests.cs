using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public sealed class QueryOptimizationPostgresFactAttribute : FactAttribute
{
    public QueryOptimizationPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CULINARY_RECIPE_TEST_POSTGRES"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CULINARY_RECIPE_TEST_REDIS")))
            Skip = "Chạy test-chung-thien-y-tuan-1-5.ps1 với PostgreSQL/Redis tạm để kiểm tra SQL và dữ liệu.";
    }
}

/// <summary>Chỉ quan sát SQL trong host test; không lưu giá trị parameter hoặc dữ liệu người dùng.</summary>
public sealed class RecipeSqlCapture : DbCommandInterceptor
{
    private ConcurrentQueue<string>? _active;

    public Capture Begin()
    {
        var commands = new ConcurrentQueue<string>();
        if (Interlocked.CompareExchange(ref _active, commands, null) is not null)
            throw new InvalidOperationException("An SQL capture is already active.");
        return new Capture(this, commands);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Volatile.Read(ref _active)?.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }

    public sealed class Capture(RecipeSqlCapture owner, ConcurrentQueue<string> commands) : IDisposable
    {
        public string[] Commands => commands.ToArray();
        public void Dispose() => Interlocked.CompareExchange(ref owner._active, null, commands);
    }
}

public partial class RecipeApiTests
{
    [QueryOptimizationPostgresFact]
    public async Task OptimizedList_ProjectsOnlySummary_AndPreservesImageFiltersPagingAndVisibility()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Danh mục projection", Slug = $"projection-{Guid.NewGuid():N}" };
        var otherAuthor = new ApplicationUser { Id = $"other-{Guid.NewGuid():N}", DisplayName = "Tác giả khác" };
        var first = OptimizationRecipe(category.Id, "A công thức");
        var second = OptimizationRecipe(category.Id, "B công thức");
        var ownerDraft = OptimizationRecipe(category.Id, "C bản nháp"); ownerDraft.Status = RecipeStatus.Draft;
        var otherDraft = OptimizationRecipe(category.Id, "D bản nháp người khác"); otherDraft.Status = RecipeStatus.Draft; otherDraft.AuthorId = otherAuthor.Id;
        var removed = OptimizationRecipe(category.Id, "E đã xóa"); removed.IsDeleted = true;
        var hard = OptimizationRecipe(category.Id, "F công thức khó"); hard.Difficulty = RecipeDifficulty.Hard;
        first.Images.Add(new RecipeImage { OriginalUrl = "https://example.test/removed.png", OrderIndex = 0, IsDeleted = true });
        first.Images.Add(new RecipeImage { OriginalUrl = "https://example.test/second.png", OrderIndex = 2, IsPrimary = true });
        first.Images.Add(new RecipeImage { OriginalUrl = "https://example.test/first.png", OrderIndex = 1 });
        await api.WithDatabaseAsync(async db =>
        {
            db.Categories.Add(category); db.Users.Add(otherAuthor);
            db.Recipes.AddRange(first, second, ownerDraft, otherDraft, removed, hard);
            await db.SaveChangesAsync();
        });

        var path = $"/api/v1/recipes?categoryId={category.Id}&difficulty=Easy&maxCookTime=30&minServings=2&sort=title&pageSize=1";
        using (var capture = api.SqlCapture.Begin())
        {
            var json = await api.GetJsonAsync(path + "&page=1");
            var item = Assert.Single(json.GetProperty("data").EnumerateArray());
            Assert.Equal(first.Id, item.GetProperty("id").GetGuid());
            Assert.Equal(category.Name, item.GetProperty("categoryName").GetString());
            Assert.Equal("Tác giả", item.GetProperty("authorDisplayName").GetString());
            // Giữ quy tắc ảnh OrderIndex thấp nhất; không âm thầm đổi sang IsPrimary.
            Assert.Equal("https://example.test/first.png", item.GetProperty("primaryImageUrl").GetString());
            Assert.Equal(2, json.GetProperty("meta").GetProperty("total").GetInt32());
            Assert.Equal(2, json.GetProperty("meta").GetProperty("totalPages").GetInt32());
            Assert.Equal(2, capture.Commands.Length); // COUNT + SELECT, không N+1 theo từng ảnh/entity.
            var sql = string.Join("\n", capture.Commands);
            foreach (var column in new[] { "\"Instructions\"", "\"Nutrition_Calories\"", "\"PasswordHash\"", "\"SecurityStamp\"", "\"MediumUrl\"" })
                Assert.DoesNotContain(column, sql);
        }

        var page2 = await api.GetJsonAsync(path + "&page=2");
        var withoutImage = Assert.Single(page2.GetProperty("data").EnumerateArray());
        Assert.Equal(second.Id, withoutImage.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, withoutImage.GetProperty("primaryImageUrl").ValueKind);
        foreach (var (token, expectedTotal) in new[] { (api.Token(), 3), (api.Token("admin", "Admin"), 4) })
        {
            using var response = await api.SendAsync(HttpMethod.Get, path + "&page=1", token: token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(expectedTotal, json.GetProperty("meta").GetProperty("total").GetInt32());
        }
    }

    [QueryOptimizationPostgresFact]
    public async Task OptimizedDetails_LoadsOrderedCollectionsWithoutCrossProduct_AndDoesNotTrackEntities()
    {
        var recipe = OptimizationRecipe(api.CategoryId, "Công thức nhiều dữ liệu con");
        recipe.Nutrition.Calories = 420;
        foreach (var number in new[] { 3, 1, 2 })
            recipe.Steps.Add(new RecipeStep { StepNumber = number, Title = $"Bước {number}", Description = "Nội dung" });
        recipe.Steps.Add(new RecipeStep { StepNumber = 4, Title = "Đã xóa", Description = "Ẩn", IsDeleted = true });
        foreach (var number in new[] { 4, 2, 3, 1 })
            recipe.Ingredients.Add(new RecipeIngredient { Name = $"Nguyên liệu {number}", OrderIndex = number });
        recipe.Ingredients.Add(new RecipeIngredient { Name = "Đã xóa", OrderIndex = 0, IsDeleted = true });
        foreach (var number in new[] { 2, 1 })
            recipe.Images.Add(new RecipeImage { OriginalUrl = $"https://example.test/{number}.png", OrderIndex = number });
        recipe.Images.Add(new RecipeImage { OriginalUrl = "https://example.test/deleted.png", OrderIndex = 0, IsDeleted = true });
        await api.WithDatabaseAsync(async db => { db.Recipes.Add(recipe); await db.SaveChangesAsync(); });

        foreach (var key in new[] { recipe.Id.ToString(), recipe.Slug })
        {
            using var capture = api.SqlCapture.Begin();
            var response = await api.GetJsonAsync($"/api/v1/recipes/{key}");
            var detail = response.GetProperty("data");
            Assert.Equal(new[] { 1, 2, 3 }, detail.GetProperty("steps").EnumerateArray().Select(x => x.GetProperty("stepNumber").GetInt32()));
            Assert.Equal(new[] { 1, 2, 3, 4 }, detail.GetProperty("ingredients").EnumerateArray().Select(x => x.GetProperty("orderIndex").GetInt32()));
            Assert.Equal(new[] { 1, 2 }, detail.GetProperty("images").EnumerateArray().Select(x => x.GetProperty("orderIndex").GetInt32()));
            Assert.Equal(420, detail.GetProperty("nutrition").GetProperty("calories").GetDecimal());
            Assert.Equal(Convert.ToBase64String(recipe.RowVersion), detail.GetProperty("rowVersion").GetString());
            Assert.Equal(4, capture.Commands.Length); // Root + Steps + Ingredients + Images.
            foreach (var command in capture.Commands)
                Assert.True(new[] { "\"RecipeSteps\"", "\"RecipeIngredients\"", "\"RecipeImages\"" }.Count(command.Contains) <= 1,
                    "A detail query must not JOIN multiple sibling collections together.");
        }
        await api.WithDatabaseAsync(async db =>
        {
            var repository = new RecipeRepository(db);
            await repository.GetByIdWithDetailsAsync(recipe.Id);
            Assert.Empty(db.ChangeTracker.Entries());
            await repository.GetBySlugWithDetailsAsync(recipe.Slug);
            Assert.Empty(db.ChangeTracker.Entries());
        });
    }

    [QueryOptimizationPostgresFact]
    public async Task OptimizedDetails_PreservesEmptyCollectionsAndDraftAccessRules()
    {
        var recipe = OptimizationRecipe(api.CategoryId, "Bản nháp trống dữ liệu con");
        recipe.Status = RecipeStatus.Draft;
        await api.WithDatabaseAsync(async db => { db.Recipes.Add(recipe); await db.SaveChangesAsync(); });
        foreach (var key in new[] { recipe.Id.ToString(), recipe.Slug })
        {
            var path = $"/api/v1/recipes/{key}";
            using var guest = await api.SendAsync(HttpMethod.Get, path);
            Assert.Equal(HttpStatusCode.Forbidden, guest.StatusCode);
            using var other = await api.SendAsync(HttpMethod.Get, path, token: api.Token("someone-else"));
            Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
            foreach (var token in new[] { api.Token(), api.Token("admin", "Admin") })
            {
                using var response = await api.SendAsync(HttpMethod.Get, path, token: token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var detail = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
                foreach (var name in new[] { "steps", "ingredients", "images" }) Assert.Empty(detail.GetProperty(name).EnumerateArray());
                Assert.Equal(recipe.Id, detail.GetProperty("id").GetGuid());
            }
        }
    }

    private static Recipe OptimizationRecipe(Guid categoryId, string title) => new()
    {
        Id = Guid.NewGuid(), Slug = $"optimization-{Guid.NewGuid():N}", Title = title,
        Description = "Mô tả công thức", Instructions = "Hướng dẫn chỉ dành cho chi tiết",
        CategoryId = categoryId, AuthorId = "author-1", Status = RecipeStatus.Published,
        PublishedAt = DateTimeOffset.UtcNow, Difficulty = RecipeDifficulty.Easy,
        PrepTime = 10, CookTime = 20, Servings = 2,
    };
}
