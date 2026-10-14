using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TestCurrentUser = CulinaryBlog.UnitTests.Recipes.TestCurrentUser;

namespace CulinaryBlog.UnitTests.Categories;

/// <summary>FR-CAT-002 — Chi tiết danh mục theo slug kèm công thức có phân trang.</summary>
public class GetCategoryBySlugQueryHandlerTests
{
    private const string AuthorA = "user-author-1";
    private const string AuthorB = "user-author-2";

    private static CulinaryBlogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CulinaryBlogDbContext(options);
    }

    private static Recipe NewRecipe(Guid categoryId, string authorId, RecipeStatus status, int index) => new()
    {
        Id = Guid.NewGuid(),
        Title = $"Món {index}",
        Slug = $"mon-{index}-{Guid.NewGuid():N}",
        Description = "Mô tả",
        Instructions = "Cách làm",
        PrepTime = 10,
        CookTime = 20,
        Servings = 2,
        AuthorId = authorId,
        CategoryId = categoryId,
        Status = status,
        PublishedAt = status == RecipeStatus.Published ? DateTimeOffset.UtcNow.AddMinutes(-index) : null,
    };

    /// <summary>
    /// Danh mục "mon-nuoc": 3 công thức Published + 1 Draft của tác giả A + 1 Draft của tác giả B
    /// + 1 Archived. Danh mục "mon-chay": 1 Published (không được lẫn sang "mon-nuoc").
    /// </summary>
    private static async Task<Category> SeedAsync(CulinaryBlogDbContext db)
    {
        var monNuoc = new Category { Id = Guid.NewGuid(), Name = "Món nước", Slug = "mon-nuoc" };
        var monChay = new Category { Id = Guid.NewGuid(), Name = "Món chay", Slug = "mon-chay" };

        db.Categories.AddRange(monNuoc, monChay);
        db.Users.AddRange(
            new ApplicationUser { Id = AuthorA, UserName = "a@dlu.edu.vn", Email = "a@dlu.edu.vn", DisplayName = "Tác giả A" },
            new ApplicationUser { Id = AuthorB, UserName = "b@dlu.edu.vn", Email = "b@dlu.edu.vn", DisplayName = "Tác giả B" });

        db.Recipes.AddRange(
            NewRecipe(monNuoc.Id, AuthorA, RecipeStatus.Published, 1),
            NewRecipe(monNuoc.Id, AuthorB, RecipeStatus.Published, 2),
            NewRecipe(monNuoc.Id, AuthorA, RecipeStatus.Published, 3),
            NewRecipe(monNuoc.Id, AuthorA, RecipeStatus.Draft, 4),
            NewRecipe(monNuoc.Id, AuthorB, RecipeStatus.Draft, 5),
            NewRecipe(monNuoc.Id, AuthorA, RecipeStatus.Archived, 6),
            NewRecipe(monChay.Id, AuthorA, RecipeStatus.Published, 7));

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return monNuoc;
    }

    private static GetCategoryBySlugQueryHandler CreateHandler(CulinaryBlogDbContext db, TestCurrentUser user) =>
        new(new CategoryRepository(db), new RecipeRepository(db), user);

    private static TestCurrentUser Anonymous() => new() { UserId = null, Role = "" };

    [Fact]
    public async Task Handle_ShouldReturnCategoryWithOnlyPublishedRecipes_WhenAnonymous()
    {
        using var db = CreateDbContext();
        var category = await SeedAsync(db);
        var handler = CreateHandler(db, Anonymous());

        var result = await handler.Handle(new GetCategoryBySlugQuery("mon-nuoc"), CancellationToken.None);

        Assert.Equal(category.Id, result.Category.Id);
        Assert.Equal("mon-nuoc", result.Category.Slug);
        Assert.Equal(3, result.Category.RecipeCount);
        Assert.Equal(3, result.Recipes.TotalCount);
        Assert.All(result.Recipes.Items, r => Assert.Equal(RecipeStatus.Published, r.Status));
    }

    [Fact]
    public async Task Handle_ShouldIncludeOwnDraftsOnly_WhenLoggedIn()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);
        var handler = CreateHandler(db, new TestCurrentUser { UserId = AuthorA });

        var result = await handler.Handle(new GetCategoryBySlugQuery("mon-nuoc"), CancellationToken.None);

        // 3 Published + 1 Draft của chính A; KHÔNG có Draft của B và KHÔNG có bài Archived.
        Assert.Equal(4, result.Recipes.TotalCount);
        Assert.Single(result.Recipes.Items, r => r.Status == RecipeStatus.Draft);
        Assert.DoesNotContain(result.Recipes.Items, r => r.Status == RecipeStatus.Archived);
        Assert.Equal(3, result.Category.RecipeCount); // số đếm công khai không đổi theo người xem
    }

    [Fact]
    public async Task Handle_ShouldPaginate()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);
        var handler = CreateHandler(db, Anonymous());

        var page1 = await handler.Handle(new GetCategoryBySlugQuery("mon-nuoc", Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await handler.Handle(new GetCategoryBySlugQuery("mon-nuoc", Page: 2, PageSize: 2), CancellationToken.None);

        Assert.Equal(2, page1.Recipes.Items.Count);
        Assert.Single(page2.Recipes.Items);
        Assert.Equal(3, page1.Recipes.TotalCount);
        Assert.Equal(2, page1.Recipes.TotalPages);
        Assert.Empty(page1.Recipes.Items.Select(x => x.Id).Intersect(page2.Recipes.Items.Select(x => x.Id)));
    }

    [Fact]
    public async Task Handle_ShouldOrderNewestPublishedFirst()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);
        var handler = CreateHandler(db, Anonymous());

        var result = await handler.Handle(new GetCategoryBySlugQuery("mon-nuoc"), CancellationToken.None);

        var publishedAt = result.Recipes.Items.Select(x => x.PublishedAt).ToList();
        Assert.Equal(publishedAt.OrderByDescending(x => x).ToList(), publishedAt);
    }

    [Fact]
    public async Task Handle_ShouldThrowCategoryNotFound_WhenSlugDoesNotExist()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);
        var handler = CreateHandler(db, Anonymous());

        await Assert.ThrowsAsync<CategoryNotFoundException>(
            () => handler.Handle(new GetCategoryBySlugQuery("khong-co"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowCategoryNotFound_WhenCategoryIsSoftDeleted()
    {
        using var db = CreateDbContext();
        var category = await SeedAsync(db);
        var tracked = await db.Categories.SingleAsync(x => x.Id == category.Id);
        tracked.IsDeleted = true;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var handler = CreateHandler(db, Anonymous());

        await Assert.ThrowsAsync<CategoryNotFoundException>(
            () => handler.Handle(new GetCategoryBySlugQuery("mon-nuoc"), CancellationToken.None));
    }
}
