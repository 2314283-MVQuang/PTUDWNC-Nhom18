using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>
/// GET /recipes?mine=true — trang quản lý "Công thức của tôi": mỗi người (Author lẫn Admin) chỉ
/// thấy công thức do chính mình đăng, gồm cả bản nháp và bản lưu trữ.
/// </summary>
public class GetMyRecipesTests
{
    private const string AuthorA = "author-a";
    private const string AuthorB = "author-b";
    private const string AdminId = "admin-1";

    private static async Task<CulinaryBlogDbContext> CreateSeededDbAsync()
    {
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CulinaryBlogDbContext(options);

        var category = new Category { Id = Guid.NewGuid(), Name = "Món nước", Slug = "mon-nuoc" };
        db.Categories.Add(category);

        foreach (var id in new[] { AuthorA, AuthorB, AdminId })
        {
            db.Users.Add(new ApplicationUser
            {
                Id = id, UserName = $"{id}@dlu.edu.vn", Email = $"{id}@dlu.edu.vn", DisplayName = id,
            });
        }

        void AddRecipe(string slug, string authorId, RecipeStatus status) => db.Recipes.Add(new Recipe
        {
            Id = Guid.NewGuid(),
            Title = slug,
            Slug = slug,
            Description = "Mô tả",
            Instructions = "Cách làm",
            PrepTime = 10,
            CookTime = 20,
            Servings = 2,
            AuthorId = authorId,
            CategoryId = category.Id,
            Status = status,
            PublishedAt = status == RecipeStatus.Draft ? null : DateTimeOffset.UtcNow,
        });

        AddRecipe("a-published", AuthorA, RecipeStatus.Published);
        AddRecipe("a-draft", AuthorA, RecipeStatus.Draft);
        AddRecipe("a-archived", AuthorA, RecipeStatus.Archived);
        AddRecipe("b-published", AuthorB, RecipeStatus.Published);
        AddRecipe("b-draft", AuthorB, RecipeStatus.Draft);
        AddRecipe("admin-published", AdminId, RecipeStatus.Published);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task<List<string>> SlugsAsync(
        CulinaryBlogDbContext db, TestCurrentUser user, bool mine, RecipeStatus? status = null)
    {
        var result = await new GetRecipesQueryHandler(new RecipeRepository(db), user)
            .Handle(new GetRecipesQuery(PageSize: 50, Mine: mine, Status: status), CancellationToken.None);
        return result.Items.Select(x => x.Slug).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public async Task Mine_ShouldReturnOnlyOwnRecipes_IncludingDraftAndArchived_ForAuthor()
    {
        await using var db = await CreateSeededDbAsync();

        var slugs = await SlugsAsync(db, new TestCurrentUser { UserId = AuthorA }, mine: true);

        Assert.Equal(new[] { "a-archived", "a-draft", "a-published" }, slugs);
    }

    [Fact]
    public async Task Mine_ShouldReturnOnlyOwnRecipes_ForAdminToo()
    {
        await using var db = await CreateSeededDbAsync();

        var slugs = await SlugsAsync(db, new TestCurrentUser { UserId = AdminId, Role = "Admin" }, mine: true);

        Assert.Equal(new[] { "admin-published" }, slugs);
    }

    [Fact]
    public async Task WithoutMine_ShouldKeepOldBehaviour_PublishedOfEveryone_PlusOwnDrafts()
    {
        await using var db = await CreateSeededDbAsync();

        var slugs = await SlugsAsync(db, new TestCurrentUser { UserId = AuthorA }, mine: false);

        Assert.Equal(new[] { "a-archived", "a-draft", "a-published", "admin-published", "b-published" }, slugs);
    }

    [Fact]
    public async Task Mine_ShouldThrowUnauthorized_WhenNotLoggedIn()
    {
        await using var db = await CreateSeededDbAsync();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => SlugsAsync(db, new TestCurrentUser { UserId = null }, mine: true));
    }

    [Fact]
    public async Task Admin_AllRecipes_ShouldSeeEveryAuthor_AndFilterByStatus()
    {
        await using var db = await CreateSeededDbAsync();
        var admin = new TestCurrentUser { UserId = AdminId, Role = "Admin" };

        var all = await SlugsAsync(db, admin, mine: false);
        var drafts = await SlugsAsync(db, admin, mine: false, status: RecipeStatus.Draft);

        Assert.Equal(6, all.Count);
        Assert.Equal(new[] { "a-draft", "b-draft" }, drafts);
    }

    [Fact]
    public async Task StatusFilter_ShouldNotWidenAccess_ForAnonymous()
    {
        await using var db = await CreateSeededDbAsync();

        var drafts = await SlugsAsync(db, new TestCurrentUser { UserId = null }, mine: false, status: RecipeStatus.Draft);

        Assert.Empty(drafts);
    }
}
