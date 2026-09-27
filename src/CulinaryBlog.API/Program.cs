using System.Text;
using System.Text.Json.Serialization;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seed;
using CulinaryBlog.Infrastructure.Services;
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

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------
app.UseMiddleware<GlobalExceptionMiddleware>(); // Phải đứng ĐẦU pipeline để bắt mọi exception phía sau.

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

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapCategoryEndpoints();

app.Run();
