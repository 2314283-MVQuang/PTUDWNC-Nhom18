using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-007 (mâu thuẫn #1) — Xoá mềm công thức.</summary>
public class DeleteRecipeTests
{
    private const string OwnerId = "user-author-1";

    private static CulinaryBlogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CulinaryBlogDbContext(options);
    }

    private static async Task<Recipe> SeedRecipeAsync(CulinaryBlogDbContext db)
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Bún bò Huế",
            Slug = "bun-bo-hue",
            Description = "Món nước miền Trung",
            Instructions = "Hầm xương, nêm mắm ruốc",
            PrepTime = 20,
            CookTime = 90,
            Servings = 4,
            AuthorId = OwnerId,
            CategoryId = Guid.NewGuid(),
            Status = RecipeStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return recipe;
    }

    private static DeleteRecipeCommandHandler CreateHandler(
        CulinaryBlogDbContext db,
        TestCurrentUser currentUser,
        TestCacheInvalidator cache) =>
        new(new RecipeRepository(db), currentUser, db, cache);

    [Fact]
    public async Task Delete_ShouldSoftDelete_SetDeletedAt_AndEvictCaches_WhenOwner()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var cache = new TestCacheInvalidator();
        var before = DateTimeOffset.UtcNow;

        await CreateHandler(db, new TestCurrentUser(), cache)
            .Handle(new DeleteRecipeCommand(recipe.Id), CancellationToken.None);

        // Global Query Filter ẩn công thức khỏi truy vấn thường...
        Assert.False(await db.Recipes.AnyAsync(x => x.Id == recipe.Id));

        // ...nhưng bản ghi vẫn còn trong DB (xoá mềm), có DeletedAt để job purge 30 ngày dùng.
        var saved = await db.Recipes.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == recipe.Id);
        Assert.True(saved.IsDeleted);
        Assert.NotNull(saved.DeletedAt);
        Assert.True(saved.DeletedAt >= before);

        Assert.Contains("recipes", cache.EvictedTags);
        Assert.Contains($"recipe-{recipe.Id}", cache.EvictedTags);
        Assert.Contains("search", cache.EvictedTags);
        Assert.Contains("categories", cache.EvictedTags);
    }

    [Fact]
    public async Task Delete_ShouldSucceed_WhenAdminIsNotOwner()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var admin = new TestCurrentUser { UserId = "admin-1", Role = "Admin" };

        await CreateHandler(db, admin, new TestCacheInvalidator())
            .Handle(new DeleteRecipeCommand(recipe.Id), CancellationToken.None);

        var saved = await db.Recipes.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == recipe.Id);
        Assert.True(saved.IsDeleted);
    }

    [Fact]
    public async Task Delete_ShouldThrowForbidden_AndKeepRecipe_WhenAnotherAuthor()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var other = new TestCurrentUser { UserId = "user-author-2" };
        var cache = new TestCacheInvalidator();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => CreateHandler(db, other, cache).Handle(new DeleteRecipeCommand(recipe.Id), CancellationToken.None));

        Assert.True(await db.Recipes.AnyAsync(x => x.Id == recipe.Id));
        Assert.Empty(cache.EvictedTags);
    }

    [Fact]
    public async Task Delete_ShouldThrowNotFound_WhenRecipeDoesNotExist()
    {
        using var db = CreateDbContext();

        await Assert.ThrowsAsync<RecipeNotFoundException>(
            () => CreateHandler(db, new TestCurrentUser(), new TestCacheInvalidator())
                .Handle(new DeleteRecipeCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ShouldThrowNotFound_WhenRecipeAlreadyDeleted()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var handler = CreateHandler(db, new TestCurrentUser(), new TestCacheInvalidator());
        await handler.Handle(new DeleteRecipeCommand(recipe.Id), CancellationToken.None);
        db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<RecipeNotFoundException>(
            () => handler.Handle(new DeleteRecipeCommand(recipe.Id), CancellationToken.None));
    }
}
