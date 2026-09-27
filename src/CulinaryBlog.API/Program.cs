using System.Text;
using System.Text.Json.Serialization;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Services;
using CulinaryBlog.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<CulinaryBlog.Infrastructure.Persistence.CulinaryBlogDbContext>();
await DbInitializer.SeedAsync(db);

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------
app.UseMiddleware<GlobalExceptionMiddleware>(); // Phải đứng ĐẦU pipeline để bắt mọi exception phía sau.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // UI tại /scalar (mục 6.2), thay Swagger UI.
}

app.UseHttpsRedirection();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles(); // Phục vụ ảnh từ LocalFileStorageService (TODO: bỏ khi chuyển sang MinIO).

app.UseAuthentication();
app.UseAuthorization();

// FR-OBS-001: TODO (nhóm làm tiếp) — thay bằng health check thật (DB + Redis + MinIO) qua
// AspNetCore.HealthChecks.NpgSql/Redis/Minio khi tích hợp các service đó.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).WithTags("Health");

app.MapAuthEndpoints();
app.MapCategoriesEndpoints();
app.MapRecipesEndpoints();

app.Run();
