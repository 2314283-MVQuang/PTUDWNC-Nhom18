using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeImage;
using CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeIngredient;
using CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeStep;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeImage;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeIngredient;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeStep;
using CulinaryBlog.Application.Features.Recipes.Commands.SetPrimaryRecipeImage;
using CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeIngredient;
using CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeStep;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeImages;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeIngredients;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeSteps;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public class TestCurrentUser : ICurrentUser
{
    public string? UserId { get; set; } = "user-author-1";
    public string? Email { get; set; } = "author@dlu.edu.vn";
    public bool IsAuthenticated => !string.IsNullOrEmpty(UserId);
    public string Role { get; set; } = "Author";
    public bool IsInRole(string role) => string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
}

public class TestCacheInvalidator : ICacheInvalidator
{
    public List<string> EvictedTags { get; } = [];

    public Task EvictByTagAsync(string tag, CancellationToken ct = default)
    {
        EvictedTags.Add(tag);
        return Task.CompletedTask;
    }
}

public class RecipeItemTests
{
    private CulinaryBlogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CulinaryBlogDbContext(options);
    }

    private async Task<Recipe> SeedRecipeAsync(CulinaryBlogDbContext db, string authorId = "user-author-1")
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Phở Bò Hà Nội",
            Slug = "pho-bo-ha-noi",
            Description = "Món phở truyền thống chuẩn vị",
            Instructions = "Nấu nước dùng rồi chần phở",
            AuthorId = authorId,
            CategoryId = Guid.NewGuid(),
            Status = RecipeStatus.Draft,
        };
        await db.Recipes.AddAsync(recipe);
        await db.SaveChangesAsync();
        return recipe;
    }

    // ==========================================================
    // 1. INGREDIENTS TESTS (FR-RCP-009)
    // ==========================================================

    [Fact]
    public async Task AddIngredient_ShouldAddSuccessfully_WhenAuthorCalls()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };
        var cacheInv = new TestCacheInvalidator();

        var handler = new AddRecipeIngredientCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db),
            db,
            currentUser,
            cacheInv);

        var command = new AddRecipeIngredientCommand(recipe.Id, "Thịt bò thăn", 0.5m, "kg", "Thái mỏng vừa ăn");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Thịt bò thăn", result.Name);
        Assert.Equal(0.5m, result.Quantity);
        Assert.Equal("kg", result.Unit);
        Assert.Equal("Thái mỏng vừa ăn", result.Notes);
        Assert.Equal(0, result.OrderIndex);

        var saved = await db.RecipeIngredients.FirstOrDefaultAsync(i => i.Id == result.Id);
        Assert.NotNull(saved);
        Assert.Contains($"recipe-{recipe.Id}", cacheInv.EvictedTags);
    }

    [Fact]
    public async Task AddIngredient_ShouldThrowForbidden_WhenOtherUserCalls()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db, "author-1");
        var currentUser = new TestCurrentUser { UserId = "other-user", Role = "Author" };

        var handler = new AddRecipeIngredientCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db),
            db,
            currentUser);

        var command = new AddRecipeIngredientCommand(recipe.Id, "Hành lá", 50, "g");
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateIngredient_ShouldUpdateSuccessfully()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var ing = new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Name = "Thịt bò",
            Quantity = 0.3m,
            Unit = "kg",
            OrderIndex = 0
        };
        await db.RecipeIngredients.AddAsync(ing);
        await db.SaveChangesAsync();

        var handler = new UpdateRecipeIngredientCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db),
            db,
            currentUser);

        var command = new UpdateRecipeIngredientCommand(recipe.Id, ing.Id, "Thịt bò nạm", 0.4m, "kg", "Cắt khối", 1);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Thịt bò nạm", result.Name);
        Assert.Equal(0.4m, result.Quantity);
        Assert.Equal("Cắt khối", result.Notes);
        Assert.Equal(1, result.OrderIndex);
    }

    [Fact]
    public async Task DeleteIngredient_ShouldSoftDelete()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var ing = new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Name = "Hạt nêm",
            OrderIndex = 0
        };
        await db.RecipeIngredients.AddAsync(ing);
        await db.SaveChangesAsync();

        var handler = new DeleteRecipeIngredientCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db),
            db,
            currentUser);

        await handler.Handle(new DeleteRecipeIngredientCommand(recipe.Id, ing.Id), CancellationToken.None);

        var inDb = await db.RecipeIngredients.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == ing.Id);
        Assert.NotNull(inDb);
        Assert.True(inDb.IsDeleted);
    }

    [Fact]
    public async Task GetIngredients_ShouldReturnOrderedList()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);

        await db.RecipeIngredients.AddRangeAsync(
            new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipe.Id, Name = "Bánh phở", OrderIndex = 2 },
            new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipe.Id, Name = "Xương bò", OrderIndex = 0 },
            new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipe.Id, Name = "Hành tây", OrderIndex = 1 }
        );
        await db.SaveChangesAsync();

        var handler = new GetRecipeIngredientsQueryHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db));

        var list = await handler.Handle(new GetRecipeIngredientsQuery(recipe.Id), CancellationToken.None);

        Assert.Equal(3, list.Count);
        Assert.Equal("Xương bò", list[0].Name);
        Assert.Equal("Hành tây", list[1].Name);
        Assert.Equal("Bánh phở", list[2].Name);
    }

    // ==========================================================
    // 2. STEPS TESTS (FR-RCP-010)
    // ==========================================================

    [Fact]
    public async Task AddStep_ShouldAutoIncrementStepNumber_WhenNotSpecified()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var handler = new AddRecipeStepCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeStep>(db),
            db,
            currentUser);

        var step1 = await handler.Handle(new AddRecipeStepCommand(recipe.Id, "Sơ chế xương", "Rửa sạch ngâm nước muối"), CancellationToken.None);
        var step2 = await handler.Handle(new AddRecipeStepCommand(recipe.Id, "Ninh xương", "Ninh nhỏ lửa 4 tiếng", TimerMinutes: 240), CancellationToken.None);

        Assert.Equal(1, step1.StepNumber);
        Assert.Equal(2, step2.StepNumber);
    }

    [Fact]
    public async Task DeleteStep_ShouldRenumberRemainingSteps()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var step1 = new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 1, Title = "B1", Description = "D1" };
        var step2 = new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 2, Title = "B2", Description = "D2" };
        var step3 = new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 3, Title = "B3", Description = "D3" };
        await db.RecipeSteps.AddRangeAsync(step1, step2, step3);
        await db.SaveChangesAsync();

        var handler = new DeleteRecipeStepCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeStep>(db),
            db,
            currentUser);

        // Xóa step 2
        await handler.Handle(new DeleteRecipeStepCommand(recipe.Id, step2.Id), CancellationToken.None);

        var remaining = await db.RecipeSteps.Where(s => s.RecipeId == recipe.Id).OrderBy(s => s.StepNumber).ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.Equal(step1.Id, remaining[0].Id);
        Assert.Equal(1, remaining[0].StepNumber);

        Assert.Equal(step3.Id, remaining[1].Id);
        Assert.Equal(2, remaining[1].StepNumber); // Đã được renumber từ 3 thành 2!
    }

    [Fact]
    public async Task UpdateStep_ShouldUpdateFieldsSuccessfully()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var step = new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 1, Title = "Title Cũ", Description = "Desc Cũ" };
        await db.RecipeSteps.AddAsync(step);
        await db.SaveChangesAsync();

        var handler = new UpdateRecipeStepCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeStep>(db),
            db,
            currentUser);

        var result = await handler.Handle(new UpdateRecipeStepCommand(recipe.Id, step.Id, "Title Mới", "Desc Mới", TimerMinutes: 15, ImageUrl: "http://minio/step.jpg"), CancellationToken.None);

        Assert.Equal("Title Mới", result.Title);
        Assert.Equal("Desc Mới", result.Description);
        Assert.Equal(15, result.TimerMinutes);
        Assert.Equal("http://minio/step.jpg", result.ImageUrl);
    }

    // ==========================================================
    // 3. IMAGES TESTS (FR-RCP-008)
    // ==========================================================

    [Fact]
    public async Task AddImage_ShouldBePrimary_WhenFirstImage()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var handler = new AddRecipeImageCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeImage>(db),
            db,
            currentUser);

        var command = new AddRecipeImageCommand(recipe.Id, "http://minio:9000/recipe-images/pho1.jpg", "Tô phở thơm ngon");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsPrimary);
    }

    [Fact]
    public async Task SetPrimaryImage_ShouldEnsureOnlyOnePrimaryImage()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var img1 = new RecipeImage { Id = Guid.NewGuid(), RecipeId = recipe.Id, OriginalUrl = "url1", IsPrimary = true };
        var img2 = new RecipeImage { Id = Guid.NewGuid(), RecipeId = recipe.Id, OriginalUrl = "url2", IsPrimary = false };
        await db.RecipeImages.AddRangeAsync(img1, img2);
        await db.SaveChangesAsync();

        var handler = new SetPrimaryRecipeImageCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeImage>(db),
            db,
            currentUser);

        var result = await handler.Handle(new SetPrimaryRecipeImageCommand(recipe.Id, img2.Id), CancellationToken.None);

        Assert.True(result.IsPrimary);

        var reloadedImg1 = await db.RecipeImages.FindAsync(img1.Id);
        var reloadedImg2 = await db.RecipeImages.FindAsync(img2.Id);

        Assert.False(reloadedImg1!.IsPrimary);
        Assert.True(reloadedImg2!.IsPrimary);
    }

    [Fact]
    public async Task DeleteImage_ShouldDesignateNextPrimary_WhenDeletedWasPrimary()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var img1 = new RecipeImage { Id = Guid.NewGuid(), RecipeId = recipe.Id, OriginalUrl = "url1", IsPrimary = true, OrderIndex = 0 };
        var img2 = new RecipeImage { Id = Guid.NewGuid(), RecipeId = recipe.Id, OriginalUrl = "url2", IsPrimary = false, OrderIndex = 1 };
        await db.RecipeImages.AddRangeAsync(img1, img2);
        await db.SaveChangesAsync();

        var handler = new DeleteRecipeImageCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeImage>(db),
            db,
            currentUser);

        await handler.Handle(new DeleteRecipeImageCommand(recipe.Id, img1.Id), CancellationToken.None);

        var reloadedImg1 = await db.RecipeImages.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == img1.Id);
        var reloadedImg2 = await db.RecipeImages.FindAsync(img2.Id);

        Assert.True(reloadedImg1!.IsDeleted);
        Assert.False(reloadedImg1.IsPrimary);
        Assert.True(reloadedImg2!.IsPrimary);
    }
}
