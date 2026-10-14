using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.UnitTests.Api;

public class SecurityInfrastructureTests
{
    [Fact]
    public void SecurityConfiguration_RejectsWeakSecretAndWildcardOrigins()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "short", ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddApiSecurity(configuration));
        configuration["Jwt:Secret"] = new string('x', 32);
        configuration["Cors:AllowedOrigins:0"] = "https://*.example.test";
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddApiSecurity(configuration));
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOriginAndIfMatch_BlocksOtherOrigins()
    {
        await using var api = await SecurityHost.StartAsync();
        foreach (var origin in new[] { "http://localhost:3000", "https://untrusted.example" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/recipes");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "PUT");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,if-match");
            using var response = await api.Client.SendAsync(request);
            if (origin == "http://localhost:3000")
            {
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
                Assert.Contains("if-match", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant());
            }
            else Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    [Fact]
    public async Task AuthRateLimit_IsSharedAcrossEndpoints_AndReturnsProblemWithRetryAfter()
    {
        await using var api = await SecurityHost.StartAsync();
        for (var i = 0; i < 10; i++)
        {
            using var response = await api.Client.PostAsJsonAsync(
                i % 2 == 0 ? "/api/v1/auth/login" : "/api/v1/auth/register",
                new { email = "invalid-email", password = "x" });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
        using var rejected = await api.Client.PostAsJsonAsync("/api/v1/auth/login", new { email = "invalid", password = "x" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta?.TotalSeconds > 0);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RATE_LIMIT_EXCEEDED", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ApiRateLimit_PartitionsByVerifiedUser_AndExcludesHealth()
    {
        await using var api = await SecurityHost.StartAsync();
        api.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.Token("a", "Author"));
        for (var i = 0; i < 100; i++)
        {
            using var response = await api.Client.GetAsync("/api/v1/security/author");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var limited = await api.Client.GetAsync("/api/v1/security/author");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        api.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.Token("b", "Author"));
        using var other = await api.Client.GetAsync("/api/v1/security/author");
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        using var health = await api.Client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Theory]
    [InlineData("missing", 401)]
    [InlineData("local", 401)]
    [InlineData("expired", 401)]
    [InlineData("issuer", 401)]
    [InlineData("audience", 401)]
    [InlineData("signature", 401)]
    [InlineData("Reader", 403)]
    [InlineData("Author", 200)]
    [InlineData("Admin", 200)]
    public async Task JwtAndPolicy_RejectInvalidTokensAndRoles(string kind, int expected)
    {
        await using var api = await SecurityHost.StartAsync();
        if (kind != "missing")
            api.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                kind == "local" ? "local-token-demo" : api.Token("user", kind));
        using var response = await api.Client.GetAsync("/api/v1/security/author");
        Assert.Equal(expected, (int)response.StatusCode);
        if (expected is 401 or 403)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(expected == 401 ? "AUTH_TOKEN_INVALID" : "FORBIDDEN", problem.GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task UploadEndpoints_RequireAuthorOrAdmin()
    {
        await using var api = await SecurityHost.StartAsync();
        using var anonymous = await api.Client.PostAsJsonAsync("/api/v1/files/presigned-url", new { fileName = "a.jpg", contentType = "image/jpeg", size = 100 });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        api.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.Token("reader", "Reader"));
        using var reader = await api.Client.GetAsync($"/api/v1/files/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, reader.StatusCode);
    }

    [Fact]
    public async Task Versioning_PreservesV1AndRejectsUnsupportedVersion()
    {
        await using var api = await SecurityHost.StartAsync();
        using var supported = await api.Client.GetAsync("/api/v1/security/public");
        Assert.Equal(HttpStatusCode.OK, supported.StatusCode);
        Assert.Contains("1.0", supported.Headers.GetValues("api-supported-versions"));
        using var unsupported = await api.Client.GetAsync("/api/v2/security/public");
        Assert.Contains(unsupported.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.NotFound });
        using var unversioned = await api.Client.GetAsync("/api/security/public");
        Assert.Equal(HttpStatusCode.NotFound, unversioned.StatusCode);
    }

    [Fact]
    public async Task OpenApi_DescribesBearerSchemasAndIfMatch_AndScalarLoads()
    {
        await using var api = await SecurityHost.StartAsync();
        var document = await api.Client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.Equal("1.0", document.GetProperty("info").GetProperty("version").GetString());
        Assert.Equal("bearer", document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        var paths = document.GetProperty("paths");
        var put = paths.GetProperty("/api/v1/recipes/{id}").GetProperty("put");
        Assert.NotEmpty(put.GetProperty("security").EnumerateArray());
        Assert.Contains(put.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "If-Match");
        Assert.True(put.GetProperty("responses").TryGetProperty("409", out _));
        var create = paths.GetProperty("/api/v1/recipes").GetProperty("post");
        Assert.True(create.GetProperty("responses").GetProperty("201").TryGetProperty("content", out _));
        Assert.False(paths.GetProperty("/api/v1/auth/login").GetProperty("post").TryGetProperty("security", out _));
        using var scalar = await api.Client.GetAsync("/scalar/v1");
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
    }

    [Fact]
    public async Task Documentation_IsNotExposedInProduction()
    {
        await using var api = await SecurityHost.StartAsync("Production");
        using var document = await api.Client.GetAsync("/openapi/v1.json");
        using var scalar = await api.Client.GetAsync("/scalar/v1");
        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
    }
}

internal sealed class SecurityHost(WebApplication app, JwtOptions jwt) : IAsyncDisposable
{
    public HttpClient Client { get; } = new() { BaseAddress = new Uri(app.Urls.Single()) };
    public static async Task<SecurityHost> StartAsync(string environment = "Development")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment, ApplicationName = typeof(RecipesEndpoints).Assembly.FullName,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        // Host chỉ kiểm tra hạ tầng HTTP, không khởi tạo handler nghiệp vụ/DB/MinIO không dùng.
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
        builder.Services.AddCulinaryOpenApi();
        builder.Services.AddHealthChecks();
        var app = builder.Build();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseRouting(); app.UseCors(); app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization();
        app.MapAuthEndpoints(); app.MapRecipeEndpoints(); app.MapFileEndpoints(); app.MapHealthEndpoints();
        app.MapV1Group("/security").MapGet("/author", () => "ok").RequireAuthorization(AuthorizationPolicies.Author);
        app.MapV1Group("/security").MapGet("/public", () => "ok");
        app.MapCulinaryApiDocumentation();
        await app.StartAsync();
        return new SecurityHost(app, jwt);
    }

    public string Token(string userId, string kind)
    {
        var options = new JwtOptions
        {
            Secret = kind == "signature" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) : jwt.Secret,
            Issuer = kind == "issuer" ? "wrong" : jwt.Issuer,
            Audience = kind == "audience" ? "wrong" : jwt.Audience,
            AccessTokenMinutes = kind == "expired" ? -5 : 15,
        };
        return new JwtService(Options.Create(options)).GenerateAccessToken(
            new ApplicationUser { Id = userId, Email = "test@example.test", DisplayName = userId }, [kind]);
    }

    public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); }
}
