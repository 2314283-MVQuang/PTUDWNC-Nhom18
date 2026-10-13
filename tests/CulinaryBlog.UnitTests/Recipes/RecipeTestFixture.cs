using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.UnitTests.Recipes;

internal sealed class RecipeTestFixture : IAsyncDisposable
{
    public CulinaryBlogDbContext Db { get; } = new(
        new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public RecipeRepository Recipes => new(Db);
    public TestCurrentUser User { get; } = new() { UserId = "author-1" };
    public TestCacheInvalidator Cache { get; } = new();

    public async Task<Recipe> SeedAsync(
        bool withSteps = true,
        bool withIngredients = true,
        RecipeStatus status = RecipeStatus.Draft)
    {
        var category = new Category { Name = "Món nước", Slug = "mon-nuoc" };
        var author = new ApplicationUser
        {
            Id = "author-1", UserName = "author-1", DisplayName = "Tác giả",
        };
        var recipe = new Recipe
        {
            Title = "Phở bò", Slug = "pho-bo", Description = "Công thức phở bò",
            Instructions = "Thực hiện theo các bước bên dưới.",
            PrepTime = 20, CookTime = 60, Servings = 4,
            Category = category, Author = author, AuthorId = author.Id,
            Status = status,
            PublishedAt = status == RecipeStatus.Published ? DateTimeOffset.UtcNow.AddDays(-1) : null,
            // InMemory không chạy trigger PostgreSQL; token này chỉ dùng cho test logic.
            RowVersion = [1, 2, 3, 4, 5, 6, 7, 8],
        };
        if (withSteps)
        {
            recipe.Steps.Add(new RecipeStep
            {
                StepNumber = 1, Title = "Nấu nước dùng", Description = "Ninh xương bò.",
            });
        }
        if (withIngredients)
        {
            recipe.Ingredients.Add(new RecipeIngredient { Name = "Thịt bò", OrderIndex = 0 });
        }
        Db.Recipes.Add(recipe);
        await Db.SaveChangesAsync();
        // Quan trọng: không để navigation đã seed che giấu lỗi thiếu Include trong repository.
        Db.ChangeTracker.Clear();
        return recipe;
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}

internal sealed class ConcurrencyFailureUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        throw new DbUpdateConcurrencyException("Một request khác đã cập nhật công thức.");

    public Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default) => action();
}
