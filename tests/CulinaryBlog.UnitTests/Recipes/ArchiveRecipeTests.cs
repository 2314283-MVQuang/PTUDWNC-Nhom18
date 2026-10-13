using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Commands.ArchiveRecipe;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-006 — Lưu trữ / bỏ lưu trữ công thức.</summary>
public class ArchiveRecipeTests
{
    private const string OwnerId = "user-author-1";

    private static CulinaryBlogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CulinaryBlogDbContext(options);
    }

    /// <summary>Recipe kèm Category + Author thật vì handler trả RecipeDetailDto (cần cả hai).</summary>
    private static async Task<Recipe> SeedRecipeAsync(
        CulinaryBlogDbContext db,
        RecipeStatus status,
        DateTimeOffset? publishedAt = null)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Món nước",
            Slug = "mon-nuoc",
        };

        var author = new ApplicationUser
        {
            Id = OwnerId,
            UserName = "author@dlu.edu.vn",
            Email = "author@dlu.edu.vn",
            DisplayName = "Tác giả",
        };

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Phở Bò Hà Nội",
            Slug = "pho-bo-ha-noi",
            Description = "Món phở truyền thống",
            Instructions = "Nấu nước dùng rồi chần phở",
            PrepTime = 10,
            CookTime = 60,
            Servings = 2,
            AuthorId = OwnerId,
            CategoryId = category.Id,
            Status = status,
            PublishedAt = publishedAt,
        };

        db.Categories.Add(category);
        db.Users.Add(author);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return recipe;
    }

    private static ArchiveRecipeCommandHandler CreateHandler(
        CulinaryBlogDbContext db,
        TestCurrentUser currentUser,
        TestCacheInvalidator cache) =>
        new(new RecipeRepository(db), currentUser, db, cache);

    [Fact]
    public async Task Archive_ShouldSetStatusArchived_AndEvictCaches_WhenOwner()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db, RecipeStatus.Published, DateTimeOffset.UtcNow.AddDays(-3));
        var cache = new TestCacheInvalidator();
        var handler = CreateHandler(db, new TestCurrentUser(), cache);

        var result = await handler.Handle(new ArchiveRecipeCommand(recipe.Id), CancellationToken.None);

        Assert.Equal(RecipeStatus.Archived, result.Status);
        var saved = await db.Recipes.AsNoTracking().SingleAsync(x => x.Id == recipe.Id);
        Assert.Equal(RecipeStatus.Archived, saved.Status);
        Assert.False(saved.IsDeleted); // lưu trữ KHÔNG phải xoá mềm
        Assert.NotNull(saved.PublishedAt); // giữ lại để bỏ lưu trữ biết đường quay về Published
        Assert.Contains("recipes", cache.EvictedTags);
        Assert.Contains($"recipe-{recipe.Id}", cache.EvictedTags);
        Assert.Contains("search", cache.EvictedTags);
        Assert.Contains("categories", cache.EvictedTags);
    }

    [Fact]
    public async Task Archive_ShouldSucceed_WhenAdminIsNotOwner()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db, RecipeStatus.Draft);
        var admin = new TestCurrentUser { UserId = "admin-1", Role = "Admin" };
        var handler = CreateHandler(db, admin, new TestCacheInvalidator());

        var result = await handler.Handle(new ArchiveRecipeCommand(recipe.Id), CancellationToken.None);

        Assert.Equal(RecipeStatus.Archived, result.Status);
    }

    [Fact]
    public async Task Archive_ShouldThrowForbidden_WhenAnotherAuthor()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db, RecipeStatus.Published, DateTimeOffset.UtcNow);
        var other = new TestCurrentUser { UserId = "user-author-2" };
        var handler = CreateHandler(db, other, new TestCacheInvalidator());

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => handler.Handle(new ArchiveRecipeCommand(recipe.Id), CancellationToken.None));

        var saved = await db.Recipes.AsNoTracking().SingleAsync(x => x.Id == recipe.Id);
        Assert.Equal(RecipeStatus.Published, saved.Status);
    }

    [Fact]
    public async Task Archive_ShouldThrowNotFound_WhenRecipeDoesNotExist()
    {
        using var db = CreateDbContext();
        var handler = CreateHandler(db, new TestCurrentUser(), new TestCacheInvalidator());

        await Assert.ThrowsAsync<RecipeNotFoundException>(
            () => handler.Handle(new ArchiveRecipeCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Archive_ShouldBeIdempotent_AndNotEvictCache_WhenAlreadyArchived()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db, RecipeStatus.Archived);
        var cache = new TestCacheInvalidator();
        var handler = CreateHandler(db, new TestCurrentUser(), cache);

        var result = await handler.Handle(new ArchiveRecipeCommand(recipe.Id), CancellationToken.None);

        Assert.Equal(RecipeStatus.Archived, result.Status);
        Assert.Empty(cache.EvictedTags);
    }

    [Fact]
    public async Task Unarchive_ShouldReturnToPublished_WhenRecipeWasPublishedBefore()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db, RecipeStatus.Archived, DateTimeOffset.UtcNow.AddDays(-3));
        var handler = CreateHandler(db, new TestCurrentUser(), new TestCacheInvalidator());

        var result = await handler.Handle(new ArchiveRecipeCommand(recipe.Id, Archive: false), CancellationToken.None);

        Assert.Equal(RecipeStatus.Published, result.Status);
    }

    [Fact]
    public async Task Unarchive_ShouldReturnToDraft_WhenRecipeWasNeverPublished()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db, RecipeStatus.Archived);
        var handler = CreateHandler(db, new TestCurrentUser(), new TestCacheInvalidator());

        var result = await handler.Handle(new ArchiveRecipeCommand(recipe.Id, Archive: false), CancellationToken.None);

        Assert.Equal(RecipeStatus.Draft, result.Status);
    }

    [Fact]
    public async Task Unarchive_ShouldDoNothing_WhenRecipeIsNotArchived()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db, RecipeStatus.Published, DateTimeOffset.UtcNow);
        var cache = new TestCacheInvalidator();
        var handler = CreateHandler(db, new TestCurrentUser(), cache);

        var result = await handler.Handle(new ArchiveRecipeCommand(recipe.Id, Archive: false), CancellationToken.None);

        Assert.Equal(RecipeStatus.Published, result.Status);
        Assert.Empty(cache.EvictedTags);
    }
}
