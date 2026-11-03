using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CulinaryBlog.UnitTests.Integration.Week8ApiFixture;

namespace CulinaryBlog.UnitTests.Integration;

[Collection("Week8Integration")]
public sealed class AuthRecipeFlowTests(Week8ApiFixture api) : IClassFixture<Week8ApiFixture>
{
    [Week8Fact]
    public async Task RegisteredAuthor_LoginRefresh_CreateUpdatePublishDelete_UsesSameIdentityAndInvalidatesCache()
    {
        using var client = api.NewClient();
        var registered = await api.RegisterAsync(client);
        var login = await LoginAsync(client, registered.Email);
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { login.RefreshToken });
        var refreshed = Week8Account.From(await DataAsync(refresh), login.Email);
        Assert.Equal(registered.UserId, refreshed.UserId);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        using var profile = await SendAsync(client, HttpMethod.Get, "/api/v1/auth/me", token: refreshed.AccessToken);
        Assert.Equal(registered.UserId, (await DataAsync(profile)).GetProperty("id").GetString());
        using var create = await SendAsync(client, HttpMethod.Post, "/api/v1/recipes", api.RecipeBody(), refreshed.AccessToken);
        var draft = await DataAsync(create, HttpStatusCode.Created);
        var id = draft.GetProperty("id").GetGuid();
        var path = $"/api/v1/recipes/{id}";
        var originalVersion = draft.GetProperty("rowVersion").GetString()!;
        Assert.Equal(registered.UserId, draft.GetProperty("authorId").GetString());
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        using var hidden = await SendAsync(client, HttpMethod.Get, path);
        await Problem(hidden, HttpStatusCode.Forbidden, "FORBIDDEN");
        using var update = await SendAsync(client, HttpMethod.Put, path,
            new { title = $"Phở bò demo đã sửa {Guid.NewGuid():N}" }, refreshed.AccessToken, originalVersion);
        var updated = await DataAsync(update);
        Assert.NotEqual(originalVersion, updated.GetProperty("rowVersion").GetString());
        using var stale = await SendAsync(client, HttpMethod.Put, path,
            new { description = "Nội dung không được ghi đè" }, refreshed.AccessToken, originalVersion);
        await Problem(stale, HttpStatusCode.Conflict, "RECIPE_CONCURRENCY_CONFLICT");

        var beforeCount = await CategoryCount(client);
        using var publish = await SendAsync(client, HttpMethod.Patch, $"{path}/publish", token: refreshed.AccessToken);
        var published = await DataAsync(publish);
        Assert.Equal("Published", published.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, published.GetProperty("publishedAt").ValueKind);
        var slugPath = $"/api/v1/recipes/{published.GetProperty("slug").GetString()}";
        // Gọi hai lần để tạo/đọc bản cache detail công khai trước DELETE.
        for (var i = 0; i < 2; i++)
        {
            using var detail = await SendAsync(client, HttpMethod.Get, slugPath);
            Assert.Equal(id, (await DataAsync(detail)).GetProperty("id").GetGuid());
        }
        Assert.Equal(beforeCount + 1, await CategoryCount(client));
        Assert.Contains(await PublicList(client), r => r.GetProperty("id").GetGuid() == id);

        using var delete = await SendAsync(client, HttpMethod.Delete, path, token: refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        foreach (var detailPath in new[] { path, slugPath })
        {
            using var detail = await SendAsync(client, HttpMethod.Get, detailPath);
            await Problem(detail, HttpStatusCode.NotFound, "RECIPE_NOT_FOUND");
        }
        Assert.DoesNotContain(await PublicList(client), r => r.GetProperty("id").GetGuid() == id);
        Assert.Equal(beforeCount, await CategoryCount(client));
        await api.InScopeAsync(async services =>
        {
            var db = services.GetRequiredService<CulinaryBlogDbContext>();
            var stored = await db.Recipes.IgnoreQueryFilters().SingleAsync(r => r.Id == id);
            Assert.True(stored.IsDeleted);
            Assert.NotNull(stored.DeletedAt);
            Assert.Equal(registered.UserId, stored.AuthorId);
            Assert.True(await db.RecipeIngredients.AnyAsync(i => i.RecipeId == id));
            Assert.True(await db.RecipeSteps.AnyAsync(s => s.RecipeId == id));
            var jwt = services.GetRequiredService<IJwtService>();
            var old = await db.RefreshTokens.SingleAsync(t => t.TokenHash == jwt.HashRefreshToken(login.RefreshToken));
            Assert.NotNull(old.RevokedAt);
            Assert.Equal(jwt.HashRefreshToken(refreshed.RefreshToken), old.ReplacedByTokenHash);
        });
    }

    [Week8Fact]
    public async Task AnotherRegisteredAuthor_CannotReadDraftUpdatePublishOrDeleteOwnerRecipe()
    {
        using var ownerClient = api.NewClient(); using var otherClient = api.NewClient();
        var owner = await api.RegisterAsync(ownerClient);
        var other = await api.RegisterAsync(otherClient);
        other = await LoginAsync(otherClient, other.Email);
        using var create = await SendAsync(ownerClient, HttpMethod.Post, "/api/v1/recipes", api.RecipeBody(), owner.AccessToken);
        var draft = await DataAsync(create, HttpStatusCode.Created);
        var id = draft.GetProperty("id").GetGuid(); var path = $"/api/v1/recipes/{id}";
        foreach (var (method, target, body) in new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Get, path, null),
            (HttpMethod.Put, path, new { title = "Không được thay đổi", authorId = other.UserId }),
            (HttpMethod.Patch, $"{path}/publish", null), (HttpMethod.Delete, path, null),
        })
        {
            using var denied = await SendAsync(otherClient, method, target, body, other.AccessToken, draft.GetProperty("rowVersion").GetString());
            await Problem(denied, HttpStatusCode.Forbidden, "FORBIDDEN");
        }
        await api.InScopeAsync(async services =>
        {
            var stored = await services.GetRequiredService<CulinaryBlogDbContext>().Recipes.SingleAsync(r => r.Id == id);
            Assert.Equal(owner.UserId, stored.AuthorId); Assert.Equal(RecipeStatus.Draft, stored.Status);
            Assert.Equal(draft.GetProperty("title").GetString(), stored.Title); Assert.False(stored.IsDeleted);
        });
    }

    [Week8Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("tampered", HttpStatusCode.Unauthorized)]
    [InlineData("no-author-role", HttpStatusCode.Forbidden)]
    public async Task Create_RejectsMissingInvalidJwtAndAccountWithoutAuthorRole(string mode, HttpStatusCode status)
    {
        using var client = api.NewClient();
        string? token = null;
        if (mode != "anonymous")
        {
            var account = await api.RegisterAsync(client);
            if (mode == "tampered")
            {
                var parts = account.AccessToken.Split('.');
                parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
                token = string.Join('.', parts);
            }
            else
            {
                await api.InScopeAsync(async services =>
                {
                    var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
                    Assert.True((await manager.RemoveFromRoleAsync((await manager.FindByIdAsync(account.UserId))!, "Author")).Succeeded);
                });
                token = (await LoginAsync(client, account.Email)).AccessToken;
            }
        }
        using var denied = await SendAsync(client, HttpMethod.Post, "/api/v1/recipes", api.RecipeBody(), token);
        await Problem(denied, status, status == HttpStatusCode.Unauthorized ? "AUTH_TOKEN_INVALID" : "FORBIDDEN");
    }

    [Week8Fact]
    public async Task AdminWithoutAuthorRole_CanUpdatePublishDeleteAnotherAuthorsRecipe()
    {
        using var client = api.NewClient();
        var owner = await api.RegisterAsync(client); var admin = await api.RegisterAsync(client);
        await api.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await manager.FindByIdAsync(admin.UserId))!;
            Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded);
            Assert.True((await manager.RemoveFromRoleAsync(user, "Author")).Succeeded);
        });
        admin = await LoginAsync(client, admin.Email);
        using var create = await SendAsync(client, HttpMethod.Post, "/api/v1/recipes", api.RecipeBody(), owner.AccessToken);
        var draft = await DataAsync(create, HttpStatusCode.Created);
        var path = $"/api/v1/recipes/{draft.GetProperty("id").GetGuid()}";
        using var update = await SendAsync(client, HttpMethod.Put, path, new { description = "Quản trị viên cập nhật" },
            admin.AccessToken, draft.GetProperty("rowVersion").GetString());
        Assert.Equal(owner.UserId, (await DataAsync(update)).GetProperty("authorId").GetString());
        using var publish = await SendAsync(client, HttpMethod.Patch, $"{path}/publish", token: admin.AccessToken);
        Assert.Equal("Published", (await DataAsync(publish)).GetProperty("status").GetString());
        using var delete = await SendAsync(client, HttpMethod.Delete, path, token: admin.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Week8Theory]
    [InlineData(false, true)] [InlineData(true, false)] [InlineData(false, false)]
    public async Task AuthenticatedOwner_CannotPublishIncompleteRecipe(bool steps, bool ingredients)
    {
        using var client = api.NewClient(); var owner = await api.RegisterAsync(client);
        using var create = await SendAsync(client, HttpMethod.Post, "/api/v1/recipes", api.RecipeBody(steps, ingredients), owner.AccessToken);
        var draft = await DataAsync(create, HttpStatusCode.Created);
        using var publish = await SendAsync(client, HttpMethod.Patch, $"/api/v1/recipes/{draft.GetProperty("id").GetGuid()}/publish", token: owner.AccessToken);
        await Problem(publish, HttpStatusCode.BadRequest, "RECIPE_PUBLISH_INCOMPLETE");
    }

    [Week8Fact]
    public async Task RefreshReuse_RevokesRotatedTokenAndOtherSessionsOfSameAccount()
    {
        using var client = api.NewClient(); var original = await api.RegisterAsync(client);
        var login = await LoginAsync(client, original.Email);
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { login.RefreshToken });
        var rotated = Week8Account.From(await DataAsync(refresh), login.Email);
        foreach (var raw in new[] { login.RefreshToken, rotated.RefreshToken, original.RefreshToken })
        {
            using var rejected = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = raw });
            await Problem(rejected, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        }
    }

    [Week8Fact]
    public async Task Logout_RevokesRefresh_ExistingAccessTokenRemainsValidUntilExpiry()
    {
        using var client = api.NewClient(); var owner = await api.RegisterAsync(client);
        using var logout = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/logout", new { owner.RefreshToken }, owner.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { owner.RefreshToken });
        await Problem(refresh, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        // JWT hiện tại là stateless; không tuyên bố logout thu hồi access token ngay lập tức.
        using var create = await SendAsync(client, HttpMethod.Post, "/api/v1/recipes", api.RecipeBody(), owner.AccessToken);
        await DataAsync(create, HttpStatusCode.Created);
    }

    [Week8Fact]
    public async Task ExpiredRefresh_IsRejected_WithoutIssuingReplacement()
    {
        using var client = api.NewClient(); var owner = await api.RegisterAsync(client);
        await api.InScopeAsync(async services =>
        {
            var db = services.GetRequiredService<CulinaryBlogDbContext>();
            var token = await db.RefreshTokens.SingleAsync(t => t.UserId == owner.UserId);
            token.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        });
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { owner.RefreshToken });
        await Problem(refresh, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        await api.InScopeAsync(async services =>
            Assert.Equal(1, await services.GetRequiredService<CulinaryBlogDbContext>().RefreshTokens.CountAsync(t => t.UserId == owner.UserId)));
    }

    private async Task<int> CategoryCount(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/categories/");
        return (await DataAsync(response)).EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == api.CategoryId)
            .GetProperty("recipeCount").GetInt32();
    }

    private static async Task<JsonElement[]> PublicList(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/recipes?page=1&pageSize=50");
        return (await DataAsync(response)).EnumerateArray().ToArray();
    }

    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, data.GetProperty("status").GetInt32());
        Assert.Equal(type, data.GetProperty("type").GetString());
    }
}
