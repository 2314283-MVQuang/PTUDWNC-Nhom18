using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.API.Services;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>
/// Gọi HTTP thật qua endpoint + MediatR + middleware + JWT.
/// Mặc định dùng InMemory; script riêng chạy lại trên PostgreSQL/Redis tạm.
/// Không chạy Program/seeder và không ghi database Docker của nhóm.
/// </summary>
public sealed class RecipeApiFixture : IAsyncLifetime
{
    private WebApplication _app = null!;
    private readonly JwtOptions _jwt = new()
    {
        Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
    };

    public HttpClient Client { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public bool UsesExternalStores { get; private set; }
    public TestCacheInvalidator? Cache { get; private set; }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(RecipesEndpoints).Assembly.FullName,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var databaseName = Guid.NewGuid().ToString();
        var postgres = Environment.GetEnvironmentVariable("CULINARY_RECIPE_TEST_POSTGRES");
        var redis = Environment.GetEnvironmentVariable("CULINARY_RECIPE_TEST_REDIS");
        UsesExternalStores = !string.IsNullOrWhiteSpace(postgres) && !string.IsNullOrWhiteSpace(redis);
        if (!string.IsNullOrWhiteSpace(postgres))
        {
            var connection = new NpgsqlConnectionStringBuilder(postgres);
            if (connection.Database?.StartsWith("culinary_recipe_tests_", StringComparison.Ordinal) != true)
                throw new InvalidOperationException("Chỉ chạy test trên database tạm culinary_recipe_tests_*.");
            builder.Services.AddDbContext<CulinaryBlogDbContext>(options => options.UseNpgsql(postgres));
        }
        else
        {
            builder.Services.AddDbContext<CulinaryBlogDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .AddInterceptors(new TestRowVersionInterceptor()));
        }
        builder.Services.AddApplication();
        builder.Services.AddCulinaryApiVersioning();
        builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
        builder.Services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));
        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CulinaryBlogDbContext>());
        if (!string.IsNullOrWhiteSpace(redis))
        {
            builder.Services.AddStackExchangeRedisOutputCache(options =>
            {
                options.Configuration = redis;
                options.InstanceName = $"culinary_recipe_tests:{databaseName}:";
            });
            builder.Services.AddOutputCache(options =>
            {
                options.AddPolicy("categories", policy => policy.Expire(TimeSpan.FromMinutes(30)).Tag("categories"));
                options.AddPolicy("Search", policy => policy.Expire(TimeSpan.FromMinutes(1)).SetVaryByQuery("*").Tag("search"));
            });
            builder.Services.AddScoped<ICacheInvalidator, OutputCacheInvalidator>();
        }
        else
        {
            Cache = new TestCacheInvalidator();
            builder.Services.AddSingleton<ICacheInvalidator>(Cache);
        }
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = _jwt.Issuer,
                ValidateAudience = true, ValidAudience = _jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)),
                ValidateLifetime = true, RoleClaimType = ClaimTypes.Role,
            });
        builder.Services.AddAuthorization(options => options.AddCulinaryBlogPolicies());
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        if (!string.IsNullOrWhiteSpace(redis)) _app.UseOutputCache();
        _app.MapRecipeEndpoints();
        if (UsesExternalStores)
        {
            _app.MapCategoryEndpoints();
            // Probe chỉ trong host test: kiểm tra evict tag recipes trên Redis thật,
            // không bổ sung cache policy/endpoint vào ứng dụng production.
            _app.MapGet("/test-cache/recipe-count", async (CulinaryBlogDbContext db) =>
                await db.Recipes.CountAsync()).CacheOutput(policy => policy.Tag("recipes"));
        }

        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            var category = new Category { Name = "Món nước", Slug = "mon-nuoc" };
            db.Categories.Add(category);
            db.Users.Add(new ApplicationUser { Id = "author-1", DisplayName = "Tác giả" });
            await db.SaveChangesAsync();
            CategoryId = category.Id;
        }
        await _app.StartAsync();
        Client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
    }

    public string Token(string userId = "author-1", string role = "Author") =>
        new JwtService(Options.Create(_jwt)).GenerateAccessToken(
            new ApplicationUser { Id = userId, Email = $"{userId}@example.test", DisplayName = userId },
            [role]);

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body = null, string? token = null, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await Client.SendAsync(request);
    }

    public async Task<JsonElement> CreateDraftAsync(
        bool withSteps = true, bool withIngredients = true, string? title = null, bool withInstructions = true)
    {
        var body = JsonSerializer.SerializeToNode(new
        {
            title = title ?? $"Phở bò {Guid.NewGuid():N}", description = "Món phở truyền thống",
            categoryId = CategoryId, prepTime = 20, cookTime = 60, servings = 4, difficulty = "Easy",
            instructions = "Thực hiện theo các bước.",
            steps = withSteps
                ? new[] { new { stepNumber = 1, title = "Ninh xương", description = "Ninh với lửa nhỏ." } }
                : [],
            ingredients = withIngredients
                ? new[] { new { name = "Thịt bò", quantity = 0.5m, unit = "kg", orderIndex = 0 } }
                : [],
        })!.AsObject();
        if (!withInstructions) body.Remove("instructions");
        using var response = await SendAsync(HttpMethod.Post, "/api/v1/recipes", body, Token());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data");
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }

    public async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task WithDatabaseAsync(Func<CulinaryBlogDbContext, Task> action)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>());
    }

    public async Task<int> CategoryRecipeCountAsync()
    {
        var json = await GetJsonAsync("/api/v1/categories/");
        return json.GetProperty("data").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == CategoryId)
            .GetProperty("recipeCount").GetInt32();
    }

    private sealed class TestRowVersionInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            // Chỉ mô phỏng trigger touch_row trong test, không thay đổi cách production sinh token.
            foreach (var entry in eventData.Context!.ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                    entry.Entity.RowVersion = RandomNumberGenerator.GetBytes(8);
            }
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }
}

public partial class RecipeApiTests(RecipeApiFixture api) : IClassFixture<RecipeApiFixture>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Phở")]
    [InlineData(" abcd ")]
    public async Task CreateAndUpdate_RejectTitlesShorterThanFiveTrimmedCharacters(string? title)
    {
        using var create = await api.SendAsync(HttpMethod.Post, "/api/v1/recipes", new
        {
            title, description = "Mô tả hợp lệ", categoryId = api.CategoryId,
            prepTime = 5, cookTime = 0, servings = 1, difficulty = "Easy",
        }, api.Token());
        var createProblem = await AssertProblem(create, HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR");
        Assert.True(createProblem.GetProperty("errors").TryGetProperty("Title", out _));

        // PUT là cập nhật từng trường: title=null nghĩa là không đổi tiêu đề.
        if (title is null) return;
        var draft = await api.CreateDraftAsync();
        using var update = await api.SendAsync(HttpMethod.Put,
            $"/api/v1/recipes/{draft.GetProperty("id").GetString()}", new { title },
            api.Token(), draft.GetProperty("rowVersion").GetString());
        var updateProblem = await AssertProblem(update, HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR");
        Assert.True(updateProblem.GetProperty("errors").TryGetProperty("Title", out _));
    }

    [Fact]
    public async Task Create_AcceptsFiveCharacterTitle_AndInvalidatesRecipesCacheAfterSave()
    {
        var evictionCount = api.Cache?.EvictedTags.Count ?? 0;
        var before = api.UsesExternalStores
            ? (await api.GetJsonAsync("/test-cache/recipe-count")).GetInt32() : 0;
        var draft = await api.CreateDraftAsync(title: "ABCDE");
        Assert.Equal("ABCDE", draft.GetProperty("title").GetString());
        if (api.Cache is not null)
            Assert.Equal(new[] { "recipes" }, api.Cache.EvictedTags.Skip(evictionCount));
        if (api.UsesExternalStores)
            Assert.Equal(before + 1, (await api.GetJsonAsync("/test-cache/recipe-count")).GetInt32());
    }

    [Fact]
    public async Task CreateAndUpdate_RejectUnknownCategoryWith422FieldError()
    {
        var categoryId = Guid.NewGuid();
        using var create = await api.SendAsync(HttpMethod.Post, "/api/v1/recipes", new
        {
            title = "Phở bò hợp lệ", description = "Mô tả hợp lệ", categoryId,
            prepTime = 5, cookTime = 0, servings = 1, difficulty = "Easy",
        }, api.Token());
        var createProblem = await AssertProblem(create, HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR");
        Assert.True(createProblem.GetProperty("errors").TryGetProperty("CategoryId", out _));

        var draft = await api.CreateDraftAsync();
        using var update = await api.SendAsync(HttpMethod.Put,
            $"/api/v1/recipes/{draft.GetProperty("id").GetString()}", new { categoryId },
            api.Token(), draft.GetProperty("rowVersion").GetString());
        var updateProblem = await AssertProblem(update, HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR");
        Assert.True(updateProblem.GetProperty("errors").TryGetProperty("CategoryId", out _));
    }

    [Fact]
    public async Task Create_WithoutLegacyInstructions_CanBeReadBackWithNullInstructions()
    {
        var created = await api.CreateDraftAsync(withInstructions: false);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("instructions").ValueKind);
        using var response = await api.SendAsync(HttpMethod.Get,
            $"/api/v1/recipes/{created.GetProperty("id").GetString()}", token: api.Token());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(JsonValueKind.Null, data.GetProperty("instructions").ValueKind);
    }

    [Fact]
    public async Task CreateReadUpdatePublish_WorksThroughHttp_AndRejectsStaleVersion()
    {
        var title = $"Phở bò {Guid.NewGuid():N}";
        var created = await api.CreateDraftAsync(title: title);
        var duplicate = await api.CreateDraftAsync(title: title);
        Assert.Equal($"{created.GetProperty("slug").GetString()}-2", duplicate.GetProperty("slug").GetString());
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal("author-1", created.GetProperty("authorId").GetString());
        var id = created.GetProperty("id").GetString();
        var path = $"/api/v1/recipes/{id}";
        var originalVersion = created.GetProperty("rowVersion").GetString();

        using var readDraft = await api.SendAsync(HttpMethod.Get, path, token: api.Token());
        Assert.Equal(HttpStatusCode.OK, readDraft.StatusCode);
        using var privateDraft = await api.SendAsync(HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.Forbidden, privateDraft.StatusCode);

        using var update = await api.SendAsync(HttpMethod.Put, path,
            new { title = $"{title} đặc biệt" }, api.Token(), originalVersion);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.NotEqual(created.GetProperty("slug").GetString(), updated.GetProperty("slug").GetString());

        using var stale = await api.SendAsync(HttpMethod.Put, path,
            new { description = "Nội dung dùng phiên bản cũ" }, api.Token(), originalVersion);
        await AssertProblem(stale, HttpStatusCode.Conflict, "RECIPE_CONCURRENCY_CONFLICT");

        var searchPath = $"/api/v1/recipes/search?q={Uri.EscapeDataString(title)}";
        var categoryCount = 0;
        if (api.UsesExternalStores)
        {
            // Prime Redis trước mutation để phát hiện thiếu evict tag search/categories.
            Assert.Empty((await api.GetJsonAsync(searchPath)).GetProperty("data").EnumerateArray());
            categoryCount = await api.CategoryRecipeCountAsync();
        }

        using var publish = await api.SendAsync(HttpMethod.Patch, $"{path}/publish", token: api.Token());
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var published = (await publish.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Published", published.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, published.GetProperty("publishedAt").ValueKind);

        using var publicDetail = await api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{published.GetProperty("slug").GetString()}");
        Assert.Equal(HttpStatusCode.OK, publicDetail.StatusCode);
        using var publicList = await api.SendAsync(HttpMethod.Get, "/api/v1/recipes?page=1&pageSize=50");
        var list = await publicList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(list.GetProperty("data").EnumerateArray(), r => r.GetProperty("id").GetString() == id);
        Assert.True(list.GetProperty("meta").GetProperty("total").GetInt32() >= 1);
        Assert.DoesNotContain(list.GetProperty("data").EnumerateArray(),
            r => r.GetProperty("id").GetString() == duplicate.GetProperty("id").GetString());
        if (api.UsesExternalStores)
        {
            Assert.Contains((await api.GetJsonAsync(searchPath)).GetProperty("data").EnumerateArray(),
                r => r.GetProperty("id").GetString() == id);
            Assert.Equal(categoryCount + 1, await api.CategoryRecipeCountAsync());
        }

        var newKeyword = Guid.NewGuid().ToString("N");
        var updatedSearchPath = $"/api/v1/recipes/search?q={newKeyword}";
        if (api.UsesExternalStores)
            Assert.Empty((await api.GetJsonAsync(updatedSearchPath)).GetProperty("data").EnumerateArray());
        using var updatePublished = await api.SendAsync(HttpMethod.Put, path,
            new { title = $"{title} đã đổi", description = $"Mô tả mới {newKeyword}" },
            api.Token(), published.GetProperty("rowVersion").GetString());
        Assert.Equal(HttpStatusCode.OK, updatePublished.StatusCode);
        var afterUpdate = (await updatePublished.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(published.GetProperty("slug").GetString(), afterUpdate.GetProperty("slug").GetString());
        if (api.UsesExternalStores)
            Assert.Contains((await api.GetJsonAsync(updatedSearchPath)).GetProperty("data").EnumerateArray(),
                r => r.GetProperty("id").GetString() == id);

        using var repeated = await api.SendAsync(HttpMethod.Patch, $"{path}/publish", token: api.Token());
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var again = (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(published.GetProperty("publishedAt").GetString(), again.GetProperty("publishedAt").GetString());
        Assert.Equal(afterUpdate.GetProperty("rowVersion").GetString(), again.GetProperty("rowVersion").GetString());
    }

    [Theory]
    [InlineData(false, true, "bước thực hiện")]
    [InlineData(true, false, "nguyên liệu")]
    [InlineData(false, false, "bước thực hiện và 1 nguyên liệu")]
    public async Task IncompletePublish_Returns400WithSpecificMissingParts(bool steps, bool ingredients, string missing)
    {
        var draft = await api.CreateDraftAsync(steps, ingredients);
        using var response = await api.SendAsync(HttpMethod.Patch,
            $"/api/v1/recipes/{draft.GetProperty("id").GetString()}/publish", token: api.Token());
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest, "RECIPE_PUBLISH_INCOMPLETE");
        Assert.Contains(missing, problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Publish_EnforcesJwtRoleAndOwnership_AndAllowsAdmin()
    {
        var draft = await api.CreateDraftAsync();
        var path = $"/api/v1/recipes/{draft.GetProperty("id").GetString()}/publish";
        using var anonymous = await api.SendAsync(HttpMethod.Patch, path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var reader = await api.SendAsync(HttpMethod.Patch, path, token: api.Token(role: "Reader"));
        Assert.Equal(HttpStatusCode.Forbidden, reader.StatusCode);
        using var otherAuthor = await api.SendAsync(HttpMethod.Patch, path, token: api.Token("other-author"));
        await AssertProblem(otherAuthor, HttpStatusCode.Forbidden, "FORBIDDEN");
        using var admin = await api.SendAsync(HttpMethod.Patch, path, token: api.Token("admin", "Admin"));
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task MissingRecipe_Returns404ProblemDetails()
    {
        using var response = await api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{Guid.NewGuid()}");
        await AssertProblem(response, HttpStatusCode.NotFound, "RECIPE_NOT_FOUND");
    }

    [Fact]
    public async Task Update_RequiresAValidIfMatchHeader()
    {
        var draft = await api.CreateDraftAsync();
        var path = $"/api/v1/recipes/{draft.GetProperty("id").GetString()}";
        using var missing = await api.SendAsync(HttpMethod.Put, path, new { title = "Tên mới" }, api.Token());
        var problem = await AssertProblem(missing, HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR");
        Assert.True(problem.GetProperty("errors").TryGetProperty("IfMatch", out _));
        using var invalid = await api.SendAsync(HttpMethod.Put, path, new { title = "Tên mới" }, api.Token(), "not-base64!");
        await AssertProblem(invalid, HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR");
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(type, json.GetProperty("type").GetString());
        Assert.Equal((int)status, json.GetProperty("status").GetInt32());
        return json;
    }
}
