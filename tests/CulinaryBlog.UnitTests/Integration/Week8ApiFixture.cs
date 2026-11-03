using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.API.Services;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seed;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using Serilog;
using Serilog.Formatting.Json;

namespace CulinaryBlog.UnitTests.Integration;

[CollectionDefinition("Week8Integration", DisableParallelization = true)]
public sealed class Week8Collection;

public sealed class Week8FactAttribute : FactAttribute
{
    public Week8FactAttribute()
    {
        if (!Week8ApiFixture.Enabled || Week8ApiFixture.Demo)
            Skip = "Chạy test-chung-thien-y-tuan-8.ps1 với PostgreSQL/Redis tạm.";
    }
}

public sealed class Week8TheoryAttribute : TheoryAttribute
{
    public Week8TheoryAttribute()
    {
        if (!Week8ApiFixture.Enabled || Week8ApiFixture.Demo)
            Skip = "Chạy test-chung-thien-y-tuan-8.ps1 với PostgreSQL/Redis tạm.";
    }
}

public sealed class Week8DemoFactAttribute : FactAttribute
{
    public Week8DemoFactAttribute()
    {
        if (!Week8ApiFixture.Enabled || !Week8ApiFixture.Demo)
            Skip = "Chế độ trình diễn riêng: test-chung-thien-y-tuan-8.ps1 -Demo.";
    }
}

/// <summary>
/// Host HTTP riêng dùng DI/endpoint/middleware nghiệp vụ thật; không gọi Program/seeder dữ liệu nhóm.
/// Identity, JWT, repository, SQL trigger, Redis và Hangfire storage đều dùng triển khai hiện có.
/// Không khởi chạy worker email/recurring job, MinIO hoặc Seq trong phạm vi Auth + Recipe.
/// </summary>
public sealed class Week8ApiFixture : IAsyncLifetime
{
    public static bool Enabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CULINARY_WEEK8_POSTGRES"));
    public static bool Demo => Environment.GetEnvironmentVariable("CULINARY_WEEK8_DEMO") == "1";
    public const string Password = "Week8Author@123";
    public WebApplication App { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public string LogDirectory { get; } = Path.Combine(Path.GetTempPath(), $"culinary-week8-logs-{Guid.NewGuid():N}");
    private int _clientNumber;

    public async Task InitializeAsync()
    {
        if (!Enabled) return;
        var postgres = Environment.GetEnvironmentVariable("CULINARY_WEEK8_POSTGRES")!;
        if (new NpgsqlConnectionStringBuilder(postgres).Database?.StartsWith("culinary_week8_tests_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Only temporary culinary_week8_tests_* databases are permitted.");
        var redis = Environment.GetEnvironmentVariable("CULINARY_WEEK8_REDIS")
            ?? throw new InvalidOperationException("Temporary Redis is required.");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Host.UseSerilog((_,_,logging) => logging.MinimumLevel.Information().Enrich.FromLogContext()
            .WriteTo.File(new JsonFormatter(), Path.Combine(LogDirectory,"system-.jsonl"), rollingInterval: RollingInterval.Day), preserveStaticLogger: true);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = postgres,
            ["Redis:ConnectionString"] = redis,
            ["Jwt:Secret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            ["Jwt:Issuer"] = "CulinaryBlog.Week8", ["Jwt:Audience"] = "CulinaryBlog.Week8.Client",
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            // Chỉ hợp lệ về cấu hình; không có worker gửi thư trong host tuần 8.
            ["WelcomeEmail:Host"] = "127.0.0.1", ["WelcomeEmail:Port"] = "1",
            ["MinIO:Endpoint"] = Environment.GetEnvironmentVariable("CULINARY_WEEK8_MINIO") ?? "127.0.0.1:1",
            ["MinIO:PublicEndpoint"] = "http://" + (Environment.GetEnvironmentVariable("CULINARY_WEEK8_MINIO") ?? "127.0.0.1:1"),
        });
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApiSecurity(builder.Configuration);
        builder.Services.AddCulinaryApiVersioning();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddStackExchangeRedisOutputCache(options =>
        {
            options.Configuration = redis;
            options.InstanceName = $"culinary_week8:{Guid.NewGuid():N}:";
        });
        builder.Services.AddScoped<ICacheInvalidator, OutputCacheInvalidator>();
        // Cùng tên/tag/TTL với Program.cs; chỉ đăng ký các policy endpoint trong host này sử dụng.
        builder.Services.AddOutputCache(options =>
        {
            options.AddPolicy("RecipeDetail", policy => policy.Expire(TimeSpan.FromMinutes(5)).Tag("recipes"));
            options.AddPolicy("categories", policy => policy.Expire(TimeSpan.FromMinutes(30)).Tag("categories"));
            options.AddPolicy("CategoryDetail", policy => policy.Expire(TimeSpan.FromMinutes(5)).SetVaryByQuery("page", "pageSize").Tag("categories", "recipes"));
            options.AddPolicy("Search", policy => policy.Expire(TimeSpan.FromMinutes(1)).SetVaryByQuery("*").Tag("search"));
        });
        builder.Services.AddHangfire(config => config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(postgres),
            new PostgreSqlStorageOptions { QueuePollInterval = TimeSpan.FromMilliseconds(200) }));
        builder.Services.AddHangfireServer(options => { options.Queues = ["recipe-images"]; options.WorkerCount = 1; options.SchedulePollingInterval = TimeSpan.FromSeconds(1); });
        App = builder.Build();
        App.UseMiddleware<CorrelationIdMiddleware>();
        App.UseSystemRequestLogging();
        App.UseMiddleware<GlobalExceptionMiddleware>();
        if (!Demo)
        {
            // Chỉ trong test tự động: mỗi HttpClient mô phỏng một IP để các ca độc lập ngân sách 10/phút.
            // Demo dùng IP thật; không thêm cơ chế này vào API production và không vô hiệu hóa limiter.
            App.Use(async (context, next) =>
            {
                if (IPAddress.TryParse(context.Request.Headers["X-Test-Client-IP"], out var address))
                    context.Connection.RemoteIpAddress = address;
                await next();
            });
        }
        App.UseRouting(); App.UseCors(); App.UseAuthentication(); App.UseRateLimiter(); App.UseAuthorization();
        App.UseOutputCache();
        App.MapAuthEndpoints(); App.MapUsersEndpoints(); App.MapRecipeEndpoints(); App.MapRecipeItemEndpoints(); App.MapCategoryEndpoints();
        await InScopeAsync(async services =>
        {
            var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
            await RoleSeeder.SeedRolesAsync(roles, services.GetRequiredService<ILogger<Week8ApiFixture>>());
            Assert.True(await roles.RoleExistsAsync("Author"));
            Assert.True(await roles.RoleExistsAsync("Admin"));
            var db = services.GetRequiredService<CulinaryBlogDbContext>();
            var category = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(db.Categories, item => item.Name == "Món nước tuần 8");
            if (category is null)
            {
                category = new Category { Name = "Món nước tuần 8", Slug = $"mon-nuoc-tuan-8-{Guid.NewGuid():N}" };
                db.Categories.Add(category);
                await db.SaveChangesAsync();
            }
            CategoryId = category.Id;
        });
        await App.StartAsync();
    }

    public HttpClient NewClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(App.Urls.Single()) };
        var number = Interlocked.Increment(ref _clientNumber);
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", $"192.0.2.{number}");
        return client;
    }

    public async Task InScopeAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = App.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    public static async Task<JsonElement> DataAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        // Không đưa response chứa token vào thông báo lỗi/log test.
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    public async Task<Week8Account> RegisterAsync(HttpClient client)
    {
        var name = $"week8{Guid.NewGuid():N}";
        var email = $"{name}@example.test";
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new
        { fullName = "Tác giả tuần 8", email, userName = name, password = Password });
        var data = await DataAsync(registered, HttpStatusCode.Created);
        Assert.Contains(data.GetProperty("user").GetProperty("roles").EnumerateArray(), r => r.GetString() == "Author");
        return Week8Account.From(data, email);
    }

    public static async Task<Week8Account> LoginAsync(HttpClient client, string email)
    {
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        return Week8Account.From(await DataAsync(login), email);
    }

    public object RecipeBody(bool steps = true, bool ingredients = true, string? title = null) => new
    {
        title = title ?? $"Phở bò tuần tám {Guid.NewGuid():N}", description = "Nấu phở bò cho bữa ăn gia đình.",
        categoryId = CategoryId, prepTime = 20, cookTime = 60, servings = 4, difficulty = "Easy",
        // Cố ý truyền một trường dư: backend phải lấy AuthorId từ JWT, không từ body.
        authorId = "forged-author-id",
        ingredients = ingredients ? new[] { new { name = "Thịt bò", quantity = 0.5m, unit = "kg", orderIndex = 0 } } : [],
        steps = steps ? new[] { new { stepNumber = 1, title = "Ninh nước dùng", description = "Ninh xương với lửa nhỏ." } } : [],
    };

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        object? body = null, string? token = null, string? version = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", version);
        return await client.SendAsync(request);
    }

    public async Task DisposeAsync()
    {
        if (App is not null) { await App.StopAsync(); await App.DisposeAsync(); }
    }
}

public sealed record Week8Account(string UserId, string Email, string AccessToken, string RefreshToken)
{
    public static Week8Account From(JsonElement data, string email) => new(
        data.GetProperty("user").GetProperty("id").GetString()!, email,
        data.GetProperty("accessToken").GetString()!, data.GetProperty("refreshToken").GetString()!);
}
