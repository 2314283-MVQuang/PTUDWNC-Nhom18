using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;
using CulinaryBlog.Domain.Common.Exceptions;
using CulinaryBlog.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public class PublishRecipeCommandHandlerTests
{
    [Fact]
    public async Task Publish_LoadsPartsAndPersistsStatus_FromFreshTrackingContext()
    {
        await using var f = new RecipeTestFixture();
        var recipe = await f.SeedAsync();
        var handler = new PublishRecipeCommandHandler(f.Recipes, f.User, f.Db, f.Cache);

        var result = await handler.Handle(new(recipe.Id), CancellationToken.None);

        Assert.Equal(RecipeStatus.Published, result.Status);
        Assert.NotNull(result.PublishedAt);
        Assert.Single(result.Steps);
        Assert.Single(result.Ingredients);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(RecipeStatus.Published, (await f.Db.Recipes.SingleAsync()).Status);
        Assert.Contains("recipes", f.Cache.EvictedTags);
        Assert.Contains($"recipe-{recipe.Id}", f.Cache.EvictedTags);
        Assert.Contains("search", f.Cache.EvictedTags);
        Assert.Contains("categories", f.Cache.EvictedTags);
    }

    [Theory]
    [InlineData(false, true, "bước thực hiện")]
    [InlineData(true, false, "nguyên liệu")]
    [InlineData(false, false, "bước thực hiện và 1 nguyên liệu")]
    public async Task Publish_RejectsIncompleteRecipe_WithoutSavingOrEvicting(
        bool withSteps, bool withIngredients, string missing)
    {
        await using var f = new RecipeTestFixture();
        var recipe = await f.SeedAsync(withSteps, withIngredients);
        var handler = new PublishRecipeCommandHandler(f.Recipes, f.User, f.Db, f.Cache);

        var error = await Assert.ThrowsAsync<RecipePublishIncompleteException>(
            () => handler.Handle(new(recipe.Id), CancellationToken.None));

        Assert.Contains(missing, error.Message);
        Assert.Empty(f.Cache.EvictedTags);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Db.Recipes.SingleAsync();
        Assert.Equal(RecipeStatus.Draft, saved.Status);
        Assert.Null(saved.PublishedAt);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Publish_IgnoresSoftDeletedParts(bool deleteStep, bool deleteIngredient)
    {
        await using var f = new RecipeTestFixture();
        var recipe = await f.SeedAsync();
        (await f.Db.RecipeSteps.SingleAsync()).IsDeleted = deleteStep;
        (await f.Db.RecipeIngredients.SingleAsync()).IsDeleted = deleteIngredient;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        var handler = new PublishRecipeCommandHandler(f.Recipes, f.User, f.Db, f.Cache);

        await Assert.ThrowsAsync<RecipePublishIncompleteException>(
            () => handler.Handle(new(recipe.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Publish_OtherAuthorIsForbidden()
    {
        await using var f = new RecipeTestFixture();
        var recipe = await f.SeedAsync();
        f.User.UserId = "other-author";
        var handler = new PublishRecipeCommandHandler(f.Recipes, f.User, f.Db, f.Cache);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => handler.Handle(new(recipe.Id), CancellationToken.None));
        Assert.Empty(f.Cache.EvictedTags);
    }

    [Fact]
    public async Task Publish_AnonymousIsUnauthorized()
    {
        await using var f = new RecipeTestFixture();
        var recipe = await f.SeedAsync();
        f.User.UserId = null;
        var handler = new PublishRecipeCommandHandler(f.Recipes, f.User, f.Db, f.Cache);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => handler.Handle(new(recipe.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Publish_AdminCanPublishAnotherAuthorsRecipe()
    {
        await using var f = new RecipeTestFixture();
        var recipe = await f.SeedAsync();
        f.User.UserId = "admin-1";
        f.User.Role = "Admin";
        var handler = new PublishRecipeCommandHandler(f.Recipes, f.User, f.Db, f.Cache);

        var result = await handler.Handle(new(recipe.Id), CancellationToken.None);

        Assert.Equal(RecipeStatus.Published, result.Status);
        Assert.Equal("author-1", result.AuthorId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_MissingOrDeletedRecipeIsNotFound(bool softDelete)
    {
        await using var f = new RecipeTestFixture();
        var id = Guid.NewGuid();
        if (softDelete)
        {
            id = (await f.SeedAsync()).Id;
            (await f.Db.Recipes.SingleAsync()).IsDeleted = true;
            await f.Db.SaveChangesAsync();
            f.Db.ChangeTracker.Clear();
        }
        var handler = new PublishRecipeCommandHandler(f.Recipes, f.User, f.Db, f.Cache);

        await Assert.ThrowsAsync<RecipeNotFoundException>(
            () => handler.Handle(new(id), CancellationToken.None));
    }

    [Fact]
    public async Task Publish_RepeatedRequestDoesNotSaveAgainOrResetPublishedAt()
    {
        await using var f = new RecipeTestFixture();
        var recipe = await f.SeedAsync(status: RecipeStatus.Published);
        // Nếu handler gọi SaveChanges lần nữa, UnitOfWork này sẽ ném lỗi.
        var handler = new PublishRecipeCommandHandler(
            f.Recipes, f.User, new ConcurrencyFailureUnitOfWork(), f.Cache);

        var result = await handler.Handle(new(recipe.Id), CancellationToken.None);

        Assert.Equal(recipe.PublishedAt, result.PublishedAt);
        Assert.Empty(f.Cache.EvictedTags);
    }

    [Fact]
    public async Task Publish_ConcurrencyFailureBecomesRecipeConflict_WithoutCacheEviction()
    {
        await using var f = new RecipeTestFixture();
        var recipe = await f.SeedAsync();
        var handler = new PublishRecipeCommandHandler(
            f.Recipes, f.User, new ConcurrencyFailureUnitOfWork(), f.Cache);

        await Assert.ThrowsAsync<RecipeConcurrencyConflictException>(
            () => handler.Handle(new(recipe.Id), CancellationToken.None));
        Assert.Empty(f.Cache.EvictedTags);
    }
}
