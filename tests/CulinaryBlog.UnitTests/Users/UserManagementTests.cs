using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Users.Commands.SetUserStatus;
using CulinaryBlog.Application.Features.Users.Queries.GetUsers;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CulinaryBlog.UnitTests.Users;

/// <summary>Tuần 9 (Quang) — trang quản lý người dùng của Admin: GET /users, PATCH /users/{id}/status.</summary>
public sealed class UserManagementTests : IAsyncLifetime
{
    private ServiceProvider services = null!;
    private CulinaryBlogDbContext db = null!;
    private UserManager<ApplicationUser> users = null!;
    private ApplicationUser admin = null!, lan = null!, minh = null!;

    public async Task InitializeAsync()
    {
        var collection = new ServiceCollection().AddLogging();
        collection.AddDataProtection();
        collection.AddDbContext<CulinaryBlogDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        collection.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CulinaryBlogDbContext>().AddDefaultTokenProviders();
        services = collection.BuildServiceProvider();
        db = services.GetRequiredService<CulinaryBlogDbContext>();
        users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("Admin"));
        await roles.CreateAsync(new IdentityRole("Author"));

        admin = await CreateUserAsync("admin@culinaryblog.vn", "Quản trị viên", -30, "Admin", "Author");
        lan = await CreateUserAsync("lan.nguyen@culinaryblog.vn", "Nguyễn Thị Lan", -20, "Author");
        minh = await CreateUserAsync("minh.tran@culinaryblog.vn", "Trần Văn Minh", -10, "Author");

        var categoryId = Guid.NewGuid();
        db.Recipes.AddRange(
            NewRecipe(lan.Id, categoryId, "Phở bò", RecipeStatus.Published),
            NewRecipe(lan.Id, categoryId, "Bánh cuốn", RecipeStatus.Draft),
            NewRecipe(lan.Id, categoryId, "Món đã xoá", RecipeStatus.Published, deleted: true),
            NewRecipe(minh.Id, categoryId, "Bún chả", RecipeStatus.Published));
        db.RefreshTokens.Add(new RefreshToken { UserId = minh.Id, TokenHash = "hash-minh", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await services.DisposeAsync();

    private async Task<ApplicationUser> CreateUserAsync(string email, string name, int days, params string[] roles)
    {
        var user = new ApplicationUser
        {
            Email = email, UserName = email.Split('@')[0], DisplayName = name,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(days),
        };
        Assert.True((await users.CreateAsync(user, "Nauan@2026")).Succeeded);
        foreach (var role in roles) await users.AddToRoleAsync(user, role);
        return user;
    }

    private static Recipe NewRecipe(string authorId, Guid categoryId, string title, RecipeStatus status, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(), Title = title, Slug = $"{Guid.NewGuid():N}", Description = title,
        AuthorId = authorId, CategoryId = categoryId, Status = status, IsDeleted = deleted,
    };

    private GetUsersQueryHandler QueryHandler() => new(users, new RepositoryBase<Recipe>(db));

    private SetUserStatusCommandHandler StatusHandler(string currentUserId) => new(
        users, new RepositoryBase<Recipe>(db), new RefreshTokenRepository(db), db,
        new FakeCurrentUser(currentUserId), NullLogger<SetUserStatusCommandHandler>.Instance);

    [Fact]
    public async Task GetUsers_ReturnsNewestFirst_WithRolesAndRecipeCounts_ExcludingDeletedRecipes()
    {
        var result = await QueryHandler().Handle(new GetUsersQuery(), CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(new[] { minh.Id, lan.Id, admin.Id }, result.Items.Select(u => u.Id));
        var lanRow = result.Items.Single(u => u.Id == lan.Id);
        Assert.Equal(2, lanRow.RecipeCount);          // "Món đã xoá" không tính
        Assert.Equal(1, lanRow.PublishedRecipeCount);
        Assert.Equal(new[] { "Author" }, lanRow.Roles);
        Assert.Equal(new[] { "Admin", "Author" }, result.Items.Single(u => u.Id == admin.Id).Roles);
        Assert.Equal(0, result.Items.Single(u => u.Id == admin.Id).RecipeCount);
    }

    [Theory]
    [InlineData("Admin", 1)]
    [InlineData("Author", 2)]
    public async Task GetUsers_FiltersByRole(string role, int expected)
    {
        var result = await QueryHandler().Handle(new GetUsersQuery(Role: role), CancellationToken.None);

        Assert.Equal(expected, result.TotalCount);
        Assert.All(result.Items, u => Assert.Equal(role == "Admin", u.Roles.Contains("Admin")));
    }

    [Theory]
    [InlineData("LAN.NGUYEN", "lan")]   // email, không phân biệt hoa thường
    [InlineData("Trần Văn", "minh")]    // tên hiển thị có dấu
    public async Task GetUsers_SearchesByEmailOrName(string keyword, string expected)
    {
        var result = await QueryHandler().Handle(new GetUsersQuery(Keyword: keyword), CancellationToken.None);

        var row = Assert.Single(result.Items);
        Assert.Equal(expected == "lan" ? lan.Id : minh.Id, row.Id);
    }

    [Fact]
    public async Task GetUsers_PaginatesAndFiltersByStatus()
    {
        minh.IsActive = false;
        await users.UpdateAsync(minh);

        var page2 = await QueryHandler().Handle(new GetUsersQuery(Page: 2, PageSize: 2), CancellationToken.None);
        Assert.Equal(3, page2.TotalCount);
        Assert.Equal(2, page2.TotalPages);
        Assert.Equal(admin.Id, Assert.Single(page2.Items).Id);

        var locked = await QueryHandler().Handle(new GetUsersQuery(IsActive: false), CancellationToken.None);
        Assert.Equal(minh.Id, Assert.Single(locked.Items).Id);
    }

    [Fact]
    public async Task SetUserStatus_LocksAccount_AndRevokesRefreshTokens_ThenUnlocks()
    {
        var locked = await StatusHandler(admin.Id).Handle(new SetUserStatusCommand(minh.Id, false), CancellationToken.None);

        Assert.False(locked.IsActive);
        Assert.Equal(1, locked.RecipeCount);
        Assert.False((await users.FindByIdAsync(minh.Id))!.IsActive);
        Assert.NotNull((await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == minh.Id)).RevokedAt);

        var unlocked = await StatusHandler(admin.Id).Handle(new SetUserStatusCommand(minh.Id, true), CancellationToken.None);
        Assert.True(unlocked.IsActive);
    }

    [Fact]
    public async Task SetUserStatus_AdminCannotLockThemselves()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            StatusHandler(admin.Id).Handle(new SetUserStatusCommand(admin.Id, false), CancellationToken.None));
        Assert.True((await users.FindByIdAsync(admin.Id))!.IsActive);
    }

    [Fact]
    public async Task SetUserStatus_UnknownUser_Throws404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            StatusHandler(admin.Id).Handle(new SetUserStatusCommand("khong-ton-tai", false), CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 51, null)]
    [InlineData(1, 20, "Reader")]
    public void GetUsersValidator_RejectsInvalidPagingAndRole(int page, int pageSize, string? role)
    {
        var result = new GetUsersQueryValidator().TestValidate(new GetUsersQuery(Role: role, Page: page, PageSize: pageSize));

        Assert.False(result.IsValid);
    }

    private sealed class FakeCurrentUser(string userId) : ICurrentUser
    {
        public string? UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == "Admin";
    }
}
