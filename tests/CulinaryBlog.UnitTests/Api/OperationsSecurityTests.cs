using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.API.Security;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Services;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.UnitTests.Api;

/// <summary>
/// Tuần 8 (Tiến) — kiểm thử phần vận hành trước khi cho người dùng thật vào:
/// Hangfire dashboard chỉ Admin, job sitemap chỉ Admin chạy tay, retry đúng SRS, health JSON
/// và readiness chỉ gồm DB + Redis.
/// </summary>
public class OperationsSecurityTests
{
    [Fact]
    public async Task HangfireDashboard_AnonymousGets401_AuthorGets403()
    {
        await using var host = await OperationsHost.StartAsync();

        using var anonymous = await host.Client.GetAsync("/hangfire");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var author = await host.Client.GetAsync($"/hangfire?access_token={host.Token("author-1", "Author")}");
        Assert.Equal(HttpStatusCode.Forbidden, author.StatusCode);

        using var fakeAdmin = await host.Client.GetAsync($"/hangfire?access_token={host.Token("admin-1", "Admin", wrongSecret: true)}");
        Assert.Equal(HttpStatusCode.Unauthorized, fakeAdmin.StatusCode);
    }

    [Fact]
    public async Task HangfireDashboard_AdminTokenInQuery_OpensDashboard_AndCookieKeepsSession()
    {
        await using var host = await OperationsHost.StartAsync();

        using var first = await host.Client.GetAsync($"/hangfire?access_token={host.Token("admin-1", "Admin")}");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var setCookie = Assert.Single(first.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(HangfireDashboardToken.CookieName + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/hangfire", setCookie, StringComparison.OrdinalIgnoreCase);

        // Trang con của dashboard (AJAX) chỉ có cookie, không có query.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/hangfire/jobs/enqueued");
        request.Headers.Add("Cookie", setCookie.Split(';')[0]);
        using var next = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task AccessTokenInQuery_IsIgnoredOutsideHangfire()
    {
        await using var host = await OperationsHost.StartAsync();

        using var viaQuery = await host.Client.GetAsync($"/api/v1/ops/admin?access_token={host.Token("admin-1", "Admin")}");
        Assert.Equal(HttpStatusCode.Unauthorized, viaQuery.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("admin-1", "Admin"));
        using var viaHeader = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, viaHeader.StatusCode);
    }

    [Fact]
    public async Task SitemapTrigger_RequiresAdmin()
    {
        await using var host = await OperationsHost.StartAsync();

        using var anonymous = await host.Client.PostAsync("/api/v1/jobs/sitemap/trigger", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs/sitemap/trigger");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("author-1", "Author"));
        using var author = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, author.StatusCode);
    }

    [Fact]
    public async Task UsersApi_OnlyAdmin_AnonymousGets401_AuthorGets403()
    {
        // Tuần 9 (Quang): trang quản lý người dùng — tác giả thường không được xem danh sách/khoá tài khoản.
        await using var host = await OperationsHost.StartAsync();

        using var anonymous = await host.Client.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        list.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("author-1", "Author"));
        using var authorList = await host.Client.SendAsync(list);
        Assert.Equal(HttpStatusCode.Forbidden, authorList.StatusCode);

        using var patch = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/users/author-2/status")
        {
            Content = new StringContent("{\"isActive\":false}", System.Text.Encoding.UTF8, "application/json"),
        };
        patch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("author-1", "Author"));
        using var authorPatch = await host.Client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.Forbidden, authorPatch.StatusCode);
    }

    [Fact]
    public async Task CommentsApi_WritingRequiresLogin()
    {
        // Tuần 9 (Quang) — FR-CMT: khách xem được bình luận nhưng không viết/sửa/xoá được.
        await using var host = await OperationsHost.StartAsync();
        var recipeId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        StringContent Body() => new("{\"content\":\"Ngon!\"}", System.Text.Encoding.UTF8, "application/json");

        using var post = await host.Client.PostAsync($"/api/v1/recipes/{recipeId}/comments", Body());
        using var put = await host.Client.PutAsync($"/api/v1/comments/{commentId}", Body());
        using var delete = await host.Client.DeleteAsync($"/api/v1/comments/{commentId}");

        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Fact]
    public void SitemapJob_RetriesTwice_PerSrs()
    {
        var retry = typeof(GenerateSitemapJob).GetCustomAttribute<AutomaticRetryAttribute>();
        Assert.NotNull(retry);
        Assert.Equal(2, retry.Attempts);
    }

    [Fact]
    public async Task Health_ReadyCoversOnlyDbAndRedis_AndAllEndpointsReturnJson()
    {
        await using var host = await OperationsHost.StartAsync(minioHealthy: false);

        using var ready = await host.Client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var readyJson = await ReadJsonAsync(ready);
        Assert.Equal("Healthy", readyJson.GetProperty("status").GetString());
        var readyChecks = readyJson.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToArray();
        Assert.Equal(new[] { "postgresql", "redis" }, readyChecks.Order());

        using var overall = await host.Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, overall.StatusCode);
        var overallJson = await ReadJsonAsync(overall);
        Assert.Equal("Unhealthy", overallJson.GetProperty("status").GetString());
        Assert.Contains(overallJson.GetProperty("checks").EnumerateArray(),
            c => c.GetProperty("name").GetString() == "minio" && c.GetProperty("status").GetString() == "Unhealthy");

        using var live = await host.Client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Empty((await ReadJsonAsync(live)).GetProperty("checks").EnumerateArray());
    }

    [Fact]
    public void HangfireAdminFilter_OnlyAuthenticatedAdmins()
    {
        Assert.False(HangfireAdminAuthorizationFilter.IsAdmin(null));
        Assert.False(HangfireAdminAuthorizationFilter.IsAdmin(new ClaimsPrincipal(new ClaimsIdentity())));
        var unauthenticatedAdmin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")]));
        Assert.False(HangfireAdminAuthorizationFilter.IsAdmin(unauthenticatedAdmin));
        var author = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Author")], "Bearer"));
        Assert.False(HangfireAdminAuthorizationFilter.IsAdmin(author));
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "Bearer"));
        Assert.True(HangfireAdminAuthorizationFilter.IsAdmin(admin));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }
}

internal sealed class OperationsHost(WebApplication app, JwtOptions jwt) : IAsyncDisposable
{
    public HttpClient Client { get; } = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    {
        BaseAddress = new Uri(app.Urls.Single()),
    };

    public static async Task<OperationsHost> StartAsync(bool minioHealthy = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing", ApplicationName = typeof(RecipesEndpoints).Assembly.FullName,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.Logging.ClearProviders();
        var jwt = new JwtOptions { Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) };
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = jwt.Secret, ["Jwt:Issuer"] = jwt.Issuer, ["Jwt:Audience"] = jwt.Audience,
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
        });
        builder.Services.AddApplication();
        builder.Services.AddApiSecurity(builder.Configuration);
        builder.Services.AddCulinaryApiVersioning();
        builder.Services.AddHangfire(config => config.UseInMemoryStorage());
        // Không resolve thật (401/403 dừng trước handler) — chỉ để minimal API nhận đúng là service.
        builder.Services.AddTransient<GenerateSitemapJob>();
        builder.Services.AddScoped<ISitemapService>(_ => throw new NotSupportedException("Không dùng trong test."));
        builder.Services.AddHealthChecks()
            .AddCheck("postgresql", () => HealthCheckResult.Healthy(), tags: ["ready"])
            .AddCheck("redis", () => HealthCheckResult.Healthy(), tags: ["ready"])
            .AddCheck("minio", () => minioHealthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("MinIO down"), tags: ["storage"]);

        var app = builder.Build();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [new HangfireAdminAuthorizationFilter()] });
        app.MapSitemapEndpoints();
        app.MapUsersEndpoints();
        app.MapCommentEndpoints();
        app.MapHealthEndpoints();
        app.MapV1Group("/ops").MapGet("/admin", () => "ok").RequireAuthorization(AuthorizationPolicies.Admin);
        await app.StartAsync();
        return new OperationsHost(app, jwt);
    }

    public string Token(string userId, string role, bool wrongSecret = false)
    {
        var options = new JwtOptions
        {
            Secret = wrongSecret ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) : jwt.Secret,
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
        };
        return new JwtService(Options.Create(options)).GenerateAccessToken(
            new ApplicationUser { Id = userId, Email = $"{userId}@example.test", DisplayName = userId }, [role]);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}
