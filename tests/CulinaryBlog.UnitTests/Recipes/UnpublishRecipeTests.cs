using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Commands.UnpublishRecipe;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-005 (Tuần 8 — Ý): huỷ xuất bản Published → Draft.</summary>
public class UnpublishRecipeTests
{
    private const string OwnerId = "user-author-1";

    private static CulinaryBlogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<CulinaryBlogDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Recipe> SeedAsync(CulinaryBlogDbContext db, RecipeStatus status)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Món nước", Slug = "mon-nuoc" };
        var author = new ApplicationUser { Id = OwnerId, UserName = "a@dlu.edu.vn", Email = "a@dlu.edu.vn", DisplayName = "Tác giả" };
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(), Title = "Phở bò", Slug = "pho-bo", Description = "Phở", Instructions = "Nấu",
            PrepTime = 10, CookTime = 60, Servings = 2, AuthorId = OwnerId, CategoryId = category.Id, Status = status,
            PublishedAt = status == RecipeStatus.Published ? DateTimeOffset.UtcNow.AddDays(-1) : null,
        };
        db.Categories.Add(category);
        db.Users.Add(author);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return recipe;
    }

    private static UnpublishRecipeCommandHandler Handler(CulinaryBlogDbContext db, TestCurrentUser user, TestCacheInvalidator cache) =>
        new(new RecipeRepository(db), user, db, cache);

    [Fact]
    public async Task Unpublish_ShouldReturnToDraft_AndEvictPublicCaches()
    {
        using var db = CreateDbContext();
        var recipe = await SeedAsync(db, RecipeStatus.Published);
        var cache = new TestCacheInvalidator();

        var result = await Handler(db, new TestCurrentUser(), cache).Handle(new UnpublishRecipeCommand(recipe.Id), CancellationToken.None);

        Assert.Equal(RecipeStatus.Draft, result.Status);
        var saved = await db.Recipes.AsNoTracking().SingleAsync(x => x.Id == recipe.Id);
        Assert.Equal(RecipeStatus.Draft, saved.Status);
        Assert.Null(saved.PublishedAt);
        Assert.Contains("recipes", cache.EvictedTags);
        Assert.Contains($"recipe-{recipe.Id}", cache.EvictedTags);
        Assert.Contains("search", cache.EvictedTags);
        Assert.Contains("categories", cache.EvictedTags);
    }

    [Fact]
    public async Task Unpublish_ShouldBeForbidden_ForAnotherAuthor()
    {
        using var db = CreateDbContext();
        var recipe = await SeedAsync(db, RecipeStatus.Published);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Handler(db, new TestCurrentUser { UserId = "user-author-2" }, new TestCacheInvalidator())
                .Handle(new UnpublishRecipeCommand(recipe.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Unpublish_ShouldDoNothing_WhenRecipeIsNotPublished()
    {
        using var db = CreateDbContext();
        var recipe = await SeedAsync(db, RecipeStatus.Draft);
        var cache = new TestCacheInvalidator();

        var result = await Handler(db, new TestCurrentUser(), cache).Handle(new UnpublishRecipeCommand(recipe.Id), CancellationToken.None);

        Assert.Equal(RecipeStatus.Draft, result.Status);
        Assert.Empty(cache.EvictedTags);
    }

    [Fact]
    public void ArchiveAfterUnpublish_ShouldNotComeBackAsPublished()
    {
        var recipe = new Recipe { Status = RecipeStatus.Published, PublishedAt = DateTimeOffset.UtcNow };

        recipe.Unpublish();
        recipe.Archive();
        recipe.Unarchive();

        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }
}
