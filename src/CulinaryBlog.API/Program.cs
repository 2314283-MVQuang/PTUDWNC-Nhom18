using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.API.Services;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Common.Interfaces;
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
            RoleClaimType = ClaimTypes.Role,
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

builder.Services.AddOutputCache();
builder.Services.AddScoped<ICacheInvalidator, OutputCacheInvalidator>();

builder.Services.AddOpenApi();

// Cho phép body JSON gửi/nhận enum dạng chuỗi (vd "Easy" thay vì số 1) — dễ đọc hơn khi test API.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ---------------------------------------------------------------------------
// FR-OBS-003 (Tuần 3): OpenTelemetry tracing cơ bản — instrument request HTTP đến (ASP.NET Core)
// và HttpClient đi ra, cộng thêm trace các câu lệnh SQL qua Npgsql (cách thực tế để "thấy" EF
// Core, vì EF Core không tự phát Activity riêng — mọi lệnh SQL cuối cùng đều qua Npgsql).
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
    var db = roleSeedScope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
    await db.Database.EnsureCreatedAsync();

    var roleManager = roleSeedScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await RoleSeeder.SeedRolesAsync(roleManager, app.Logger);

    var userManager = roleSeedScope.ServiceProvider.GetRequiredService<UserManager<CulinaryBlog.Domain.Entities.ApplicationUser>>();
    await RoleSeeder.SeedDefaultAdminAsync(userManager, app.Logger);
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
    }
}

app.UseHttpsRedirection();
app.UseCors();
app.UseOutputCache();

app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapHealthEndpoints();
app.MapCategoryEndpoints();

app.Run();
