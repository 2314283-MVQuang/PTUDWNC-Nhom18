using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Comments.Commands.AddComment;
using CulinaryBlog.Application.Features.Comments.Commands.DeleteComment;
using CulinaryBlog.Application.Features.Comments.Commands.UpdateComment;
using CulinaryBlog.Application.Features.Comments.Queries.GetRecipeComments;
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

namespace CulinaryBlog.UnitTests.Comments;

/// <summary>Tuần 9 (Quang) — FR-CMT-001..004: xem, viết, sửa, xoá bình luận công thức.</summary>
public sealed class RecipeCommentTests : IAsyncLifetime
{
    private ServiceProvider services = null!;
    private CulinaryBlogDbContext db = null!;
    private UserManager<ApplicationUser> users = null!;
    private ApplicationUser lan = null!, minh = null!, admin = null!;
    private Recipe published = null!, draft = null!;

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

        lan = await CreateUserAsync("lan@culinaryblog.vn", "Nguyễn Thị Lan");
        minh = await CreateUserAsync("minh@culinaryblog.vn", "Trần Văn Minh");
        admin = await CreateUserAsync("admin@culinaryblog.vn", "Quản trị viên");

        var categoryId = Guid.NewGuid();
        published = NewRecipe(lan.Id, categoryId, "Phở bò", RecipeStatus.Published);
        draft = NewRecipe(lan.Id, categoryId, "Bánh cuốn nháp", RecipeStatus.Draft);
        db.Recipes.AddRange(published, draft);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await services.DisposeAsync();

    private async Task<ApplicationUser> CreateUserAsync(string email, string name)
    {
        var user = new ApplicationUser { Email = email, UserName = email.Split('@')[0], DisplayName = name };
        Assert.True((await users.CreateAsync(user, "Nauan@2026")).Succeeded);
        return user;
    }

    private static Recipe NewRecipe(string authorId, Guid categoryId, string title, RecipeStatus status) => new()
    {
        Id = Guid.NewGuid(), Title = title, Slug = $"{Guid.NewGuid():N}", Description = title,
        AuthorId = authorId, CategoryId = categoryId, Status = status,
    };

    private AddCommentCommandHandler Add(ICurrentUser user) =>
        new(new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeComment>(db), db, users, user);

    private GetRecipeCommentsQueryHandler Get(ICurrentUser user) =>
        new(new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeComment>(db), user);

    private UpdateCommentCommandHandler Update(ICurrentUser user) => new(new RepositoryBase<RecipeComment>(db), db, user);

    private DeleteCommentCommandHandler Delete(ICurrentUser user) =>
        new(new RepositoryBase<RecipeComment>(db), db, user, NullLogger<DeleteCommentCommandHandler>.Instance);

    private static FakeUser As(ApplicationUser u, bool isAdmin = false) => new(u.Id, isAdmin);
    private static readonly FakeUser Guest = new(null, false);

    [Fact]
    public async Task AddComment_OnPublishedRecipe_TrimsContent_AndGuestCanReadIt()
    {
        var created = await Add(As(minh)).Handle(new AddCommentCommand(published.Id, "  Ngon quá, cảm ơn chị Lan!  "), CancellationToken.None);

        Assert.Equal("Ngon quá, cảm ơn chị Lan!", created.Content);
        Assert.Equal("Trần Văn Minh", created.Author.DisplayName);
        Assert.True(created.CanEdit);
        Assert.False(created.IsEdited);

        var page = await Get(Guest).Handle(new GetRecipeCommentsQuery(published.Id), CancellationToken.None);
        var row = Assert.Single(page.Items);
        Assert.Equal(created.Id, row.Id);
        Assert.False(row.CanEdit);   // khách không sửa/xoá được
        Assert.False(row.CanDelete);
    }

    [Fact]
    public async Task GetComments_NewestFirst_WithPaging()
    {
        foreach (var text in new[] { "Bình luận 1", "Bình luận 2", "Bình luận 3" })
        {
            await Add(As(minh)).Handle(new AddCommentCommand(published.Id, text), CancellationToken.None);
            await Task.Delay(5);
        }

        var first = await Get(Guest).Handle(new GetRecipeCommentsQuery(published.Id, 1, 2), CancellationToken.None);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(new[] { "Bình luận 3", "Bình luận 2" }, first.Items.Select(c => c.Content));
        var second = await Get(Guest).Handle(new GetRecipeCommentsQuery(published.Id, 2, 2), CancellationToken.None);
        Assert.Equal("Bình luận 1", Assert.Single(second.Items).Content);
    }

    [Fact]
    public async Task Comments_OnDraft_AreHiddenFromStrangers_AndCannotBeAdded()
    {
        await Assert.ThrowsAsync<RecipeNotFoundException>(() =>
            Get(Guest).Handle(new GetRecipeCommentsQuery(draft.Id), CancellationToken.None));
        await Assert.ThrowsAsync<RecipeNotFoundException>(() =>
            Add(As(minh)).Handle(new AddCommentCommand(draft.Id, "Xem trộm"), CancellationToken.None));
        // Chủ bản nháp xem được (danh sách rỗng) nhưng không bình luận được khi chưa xuất bản.
        Assert.Empty((await Get(As(lan)).Handle(new GetRecipeCommentsQuery(draft.Id), CancellationToken.None)).Items);
        await Assert.ThrowsAsync<ConflictException>(() =>
            Add(As(lan)).Handle(new AddCommentCommand(draft.Id, "Ghi chú"), CancellationToken.None));
    }

    [Fact]
    public async Task AddComment_ByLockedAccount_IsForbidden()
    {
        minh.IsActive = false;
        await users.UpdateAsync(minh);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Add(As(minh)).Handle(new AddCommentCommand(published.Id, "Spam"), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateComment_OnlyByWriter_MarksEdited()
    {
        var c = await Add(As(minh)).Handle(new AddCommentCommand(published.Id, "Nước dùng hơi mặn"), CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Update(As(lan)).Handle(new UpdateCommentCommand(c.Id, "Sửa lời người khác"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Update(As(admin, isAdmin: true)).Handle(new UpdateCommentCommand(c.Id, "Admin sửa"), CancellationToken.None));

        var updated = await Update(As(minh)).Handle(new UpdateCommentCommand(c.Id, "Nước dùng vừa ăn"), CancellationToken.None);
        Assert.Equal("Nước dùng vừa ăn", updated.Content);
        Assert.True(updated.IsEdited);
    }

    [Theory]
    [InlineData("writer")]
    [InlineData("recipeOwner")]
    [InlineData("admin")]
    public async Task DeleteComment_ByWriterRecipeOwnerOrAdmin_SoftDeletes(string who)
    {
        var c = await Add(As(minh)).Handle(new AddCommentCommand(published.Id, "Cảm ơn!"), CancellationToken.None);
        var actor = who switch { "writer" => As(minh), "recipeOwner" => As(lan), _ => As(admin, isAdmin: true) };

        await Delete(actor).Handle(new DeleteCommentCommand(c.Id), CancellationToken.None);

        Assert.Empty((await Get(Guest).Handle(new GetRecipeCommentsQuery(published.Id), CancellationToken.None)).Items);
        Assert.True((await db.RecipeComments.IgnoreQueryFilters().SingleAsync(x => x.Id == c.Id)).IsDeleted);
    }

    [Fact]
    public async Task DeleteComment_ByStranger_IsForbidden_AndMissingIs404()
    {
        var c = await Add(As(lan)).Handle(new AddCommentCommand(published.Id, "Chủ bài trả lời"), CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Delete(As(minh)).Handle(new DeleteCommentCommand(c.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Delete(As(admin, isAdmin: true)).Handle(new DeleteCommentCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void AddCommentValidator_RejectsBlank(string content) =>
        new AddCommentCommandValidator().TestValidate(new AddCommentCommand(Guid.NewGuid(), content))
            .ShouldHaveValidationErrorFor(x => x.Content);

    [Fact]
    public void AddCommentValidator_Max1000CharsAfterTrim()
    {
        var validator = new AddCommentCommandValidator();
        validator.TestValidate(new AddCommentCommand(Guid.NewGuid(), $"  {new string('a', 1000)}  "))
            .ShouldNotHaveValidationErrorFor(x => x.Content);
        validator.TestValidate(new AddCommentCommand(Guid.NewGuid(), new string('a', 1001)))
            .ShouldHaveValidationErrorFor(x => x.Content);
    }

    private sealed class FakeUser(string? userId, bool isAdmin) : ICurrentUser
    {
        public string? UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => userId is not null;
        public bool IsInRole(string role) => isAdmin && role == "Admin";
    }
}
