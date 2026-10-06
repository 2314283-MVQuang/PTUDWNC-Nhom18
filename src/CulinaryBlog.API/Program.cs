using System.Text;
using System.Text.Json.Serialization;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seed;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

// Bootstrap logger: bắt lỗi xảy ra TRƯỚC khi builder.Host.UseSerilog() đọc được cấu hình từ
// appsettings (vd sai connection string lúc AddDbContext) — không có bootstrap logger thì lỗi
// giai đoạn này chỉ hiện ra console mặc định, không ghi log có cấu trúc được.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// FR-OBS-002 (Tuần 3): Serilog thay Microsoft.Extensions.Logging mặc định — đọc cấu hình từ
// section "Serilog" trong appsettings.json (sink Console + Seq, xem docker-compose.yml service
// "seq"). ReadFrom.Services(services) cho phép Serilog dùng Enricher đăng ký qua DI nếu cần sau.
// ---------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---------------------------------------------------------------------------
// Đăng ký service — mỗi tầng có 1 extension method DI riêng (mục 6.2: Program.cs +
// extension methods AddApplication/AddInfrastructure/AddPresentation).
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// Tuần 4: Output Cache backed bởi Redis (mâu thuẫn #3 — chỉ 1 tầng cache duy nhất, bỏ hẳn
// IMemoryCache/CachingBehavior/CacheInvalidationBehavior khỏi mọi thiết kế về sau). Đăng ký ở đây
// (API layer, Sdk.Web) chứ không phải Infrastructure — Infrastructure là class library thường,
// không chắc có sẵn shared framework ASP.NET Core mà AddOutputCache()/OutputCachePolicyBuilder
// cần. Policy TTL lấy đúng NFR-PERF-003 làm chuẩn duy nhất; người làm Category/Recipe/Search chỉ
// cần gắn [OutputCache(PolicyName = "categories"|"RecipeDetail"|"Search")] (hoặc .CacheOutput(...)
// như CategoryEndpoints.cs) lên endpoint GET của mình — KHÔNG tự cấu hình cache riêng. Invalidate
// khi Create/Update/Delete: inject IOutputCacheStore rồi gọi
// EvictByTagAsync("categories"|"recipes"|"search", ct) trong Command Handler tương ứng (tag đặt
// sẵn trong policy dưới đây).
// ---------------------------------------------------------------------------
var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? throw new InvalidOperationException(
        "Thiếu cấu hình Redis:ConnectionString (xem appsettings.json / docker-compose.yml service \"redis\").");

builder.Services.AddStackExchangeRedisOutputCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "culinaryblog:";
});

builder.Services.AddOutputCache(options =>
{
    // Category list: TTL 30 phút (NFR-PERF-003, "ít thay đổi"). Tên policy "categories" (chữ
    // thường) khớp với .CacheOutput("categories") ở CategoryEndpoints.cs (FR-CAT-001) — ĐỪNG đổi
    // tên nếu không sửa luôn bên đó.
    options.AddPolicy("categories", policy => policy
        .Expire(TimeSpan.FromMinutes(30))
        .Tag("categories"));

    // Recipe detail: TTL 5 phút (NFR-PERF-003, cache-aside pattern).
    options.AddPolicy("RecipeDetail", policy => policy
        .Expire(TimeSpan.FromMinutes(5))
        .Tag("recipes"));

    // Search: TTL 1 phút, vary theo TOÀN BỘ query string (q, category, sort, page...) — 2 query
    // khác nhau không được dùng chung 1 bản cache (NFR-PERF-003 + mâu thuẫn #3).
    options.AddPolicy("Search", policy => policy
        .Expire(TimeSpan.FromMinutes(1))
        .SetVaryByQuery("*")
        .Tag("search"));
});

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtSecret = jwtSection["Secret"]
    ?? throw new InvalidOperationException(
        "Thiếu cấu hình Jwt:Secret. Chạy 'dotnet user-secrets set \"Jwt:Secret\" \"<chuỗi bí mật dài>\"' " +
        "trong thư mục src/CulinaryBlog.API (CONS mục 5.2: không commit secret vào Git).");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization(options => options.AddCulinaryBlogPolicies());

// TODO (nhóm làm tiếp): CORS hiện cho phép origin frontend từ appsettings ("Cors:AllowedOrigins"),
// KHÔNG BAO GIỜ dùng AllowAnyOrigin() ở production (mục 5.2 — không wildcard "*").
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddOpenApi();

// Cho phép body JSON gửi/nhận enum dạng chuỗi (vd "Easy" thay vì số 1) — dễ đọc hơn khi test API.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ---------------------------------------------------------------------------
// FR-OBS-003 (Tuần 3): OpenTelemetry tracing cơ bản — instrument request HTTP đến (ASP.NET Core)
// và HttpClient đi ra, cộng thêm trace các câu lệnh SQL qua Npgsql (cách thực tế để "thấy" EF
// Core, vì EF Core không tự phát Activity riêng — mọi lệnh SQL cuối cùng đều qua Npgsql). Npgsql
// tự phát Activity dưới ActivitySource tên "Npgsql" SẴN (từ Npgsql 6+, không cần gói riêng) —
// AddSource("Npgsql") chỉ là "đăng ký nghe" nguồn đó, KHÔNG dùng AddNpgsql() (tên đó là của EF Core
// Npgsql.EntityFrameworkCore.PostgreSQL, dùng để đăng ký DbContext, khác hoàn toàn mục đích).
// Có cấu hình "OpenTelemetry:OtlpEndpoint" (vd collector/Tempo) → xuất qua OTLP; KHÔNG có (mặc
// định máy dev chưa dựng collector) → in trace ra console để vẫn xem được, không mất tác dụng.
// ---------------------------------------------------------------------------
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "CulinaryBlog.API"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Npgsql");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
        else
        {
            tracing.AddConsoleExporter();
        }
    });

var app = builder.Build();

// ---------------------------------------------------------------------------
// RoleSeeder (Tuần 3 — "phân quyền Role"): tạo sẵn role "Admin"/"Author" trong AspNetRoles nếu
// CHƯA có. PHẢI chạy ở MỌI environment (khác DbSeeder ở dưới, chỉ chạy Development) — thiếu
// bước này thì RegisterCommandHandler.AddToRoleAsync(user, "Author") sẽ lỗi ngay cả ở production.
// ---------------------------------------------------------------------------
using (var roleSeedScope = app.Services.CreateScope())
{
    var roleManager = roleSeedScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await RoleSeeder.SeedRolesAsync(roleManager, app.Logger);
}

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------
app.UseMiddleware<CorrelationIdMiddleware>(); // Đứng TRƯỚC GlobalException — request lỗi 500 vẫn cần có CorrelationId trong log.
app.UseMiddleware<GlobalExceptionMiddleware>(); // Phải đứng ĐẦU pipeline (sau CorrelationId) để bắt mọi exception phía sau.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // UI tại /scalar (mục 6.2), thay Swagger UI.

    // Sinh dữ liệu mẫu ngẫu nhiên bằng Bogus (chỉ ở Development, KHÔNG bao giờ chạy ở production).
    // DbSeeder tự kiểm tra số lượng hiện có và chỉ chèn thêm cho tới khi đạt tối thiểu 20
    // categories / 100 recipes (mỗi recipe >= 10 nguyên liệu, >= 5 bước) — xem
    // Infrastructure/Persistence/Seed/DbSeeder.cs. An toàn khi chạy lại nhiều lần.
    // GIỮ LẠI theo yêu cầu Lab: đảm bảo database luôn có đủ dữ liệu mẫu, dù các module CRUD
    // Category/Recipe (FR-CAT, FR-RCP...) đã tạm gỡ khỏi phạm vi triển khai để phân công lại.
    using (var seedScope = app.Services.CreateScope())
    {
        var seedContext = seedScope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        await DbSeeder.SeedRandomDataAsync(seedContext, app.Logger);

        // Tài khoản Admin THẬT trong DB (chỉ Development) để test API cần quyền Admin.
        var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await RoleSeeder.SeedDevAdminAsync(userManager, app.Logger);
    }
}

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Tuần 4: Output Cache middleware (backend Redis, đăng ký ở khối AddStackExchangeRedisOutputCache/
// AddOutputCache phía trên). Đứng SAU UseAuthorization để endpoint cache vẫn tôn trọng phân quyền
// trước khi phục vụ từ cache — chỉ endpoint nào tự gắn [OutputCache(PolicyName = "...")] mới bị
// cache, các endpoint Auth/health ở dưới KHÔNG bị ảnh hưởng vì không gắn policy nào.
app.UseOutputCache();

app.MapAuthEndpoints();
app.MapRecipeEndpoints();
app.MapCategoryEndpoints();
app.MapHealthEndpoints();

app.Run();
