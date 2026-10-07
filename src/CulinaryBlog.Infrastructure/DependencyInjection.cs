using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.Infrastructure;

/// <summary>Điểm đăng ký DI duy nhất của Infrastructure Layer — API layer chỉ cần gọi AddInfrastructure().</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // --- Database (CONS-006: PostgreSQL duy nhất, EF Core Code-First) ---
        services.AddDbContext<CulinaryBlogDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CulinaryBlogDbContext>());

        // --- Repositories ---
        // CHỈ giữ lại những gì FR-AUTH cần. ICategoryRepository đã gỡ cùng module
        // Category/Recipe (xem ghi chú trong Program.cs) — sẽ đăng ký lại khi nhóm triển khai tiếp.
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        // FR-CAT-005 (Thịnh): kiểm tra danh mục còn công thức trước khi xoá.
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        // FR-CAT-003/004 (Tiến): sinh slug duy nhất cho Category (tự thêm hậu tố -2, -3...).
        services.AddScoped<ISlugGenerator, SlugGenerator>();

        // Repository generic cho các entity con (RecipeStep/RecipeIngredient/RecipeImage): Handler
        // chỉ cần Remove()/AddAsync() một dòng con nên không đáng viết repository chuyên biệt cho
        // từng loại. Đăng ký open generic: xin IRepository<T> sẽ nhận RepositoryBase<T>. Giữ lại vì
        // là hạ tầng dùng chung, không gắn riêng với module nào.
        services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));

        // --- ASP.NET Core Identity (CONS-004: PBKDF2, mục 5.2: policy mật khẩu + khóa 5 lần sai) ---
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireLowercase = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CulinaryBlogDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // --- JWT / Current user ---
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IJwtService, JwtService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();

        // --- Google OAuth (FR-AUTH-003) ---
        // Xác thực Google ID Token mà frontend (Auth.js) gửi lên POST /auth/google. "Google:ClientId"
        // (appsettings.json) PHẢI trùng AUTH_GOOGLE_ID bên frontend (.env.local) — đây là "audience"
        // Google gắn vào token lúc phát hành, sai giá trị này thì token hợp lệ vẫn bị từ chối.
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        // --- Email: FR-AUTH-001 (Register) gửi email chào mừng qua service này; Tuần 3 thêm
        // reset mật khẩu + xác nhận email (cùng interface IEmailService) ---
        // IFileStorageService đã gỡ cùng module Recipe (upload ảnh công thức, không thuộc FR-AUTH).
        services.AddScoped<IEmailService, ConsoleEmailService>();

        // --- UploadedFile Repository & MinIO Storage (FR-FILE-001, Tiến) ---
        services.AddScoped<IUploadedFileRepository, UploadedFileRepository>();
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        // --- Redis (Tuần 4) ---
        // Mâu thuẫn #3 (xem phan-tich-mau-thuan-SRS): chỉ dùng 1 tầng cache duy nhất — ASP.NET Core
        // Output Cache middleware ở Presentation layer, backend bằng Redis (NFR-SCALE-001: cấm
        // IMemoryCache vì không "distributed" khi chạy nhiều instance). KHÔNG dùng
        // CachingBehavior/CacheInvalidationBehavior trong MediatR pipeline nữa — 2 lớp đó CHƯA từng
        // được viết trong repo này (module Category/Recipe chưa code tới lúc gỡ mâu thuẫn), nên
        // không có gì phải xoá, chỉ cần KHÔNG ai viết lại kiểu cache đó khi làm Category/Recipe sau.
        // AddStackExchangeRedisOutputCache() + AddOutputCache() (policy TTL theo NFR-PERF-003) đăng
        // ký ở API/Program.cs, không phải ở đây — 2 API đó thuộc ASP.NET Core shared framework, chỉ
        // Sdk.Web (project API) chắc chắn có sẵn, project Infrastructure này là class library
        // thường (Sdk "Microsoft.NET.Sdk"). Ở đây chỉ giữ connection string dùng chung cho health
        // check Redis ngay dưới.
        var redisConnectionString = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Thiếu cấu hình Redis:ConnectionString (xem appsettings.json / docker-compose.yml service \"redis\").");

        // --- Health checks (FR-OBS-001, Tuần 3 + Tuần 4) ---
        // "postgresql"/"redis" gắn tag "ready" — dùng cho /health/ready (app đã sẵn sàng nhận
        // traffic chưa, CÓ kiểm tra dependency ngoài). /health/live KHÔNG chạy check nào (xem
        // API/Endpoints/HealthEndpoints.cs) nên không cần đăng ký gì thêm ở đây cho liveness.
        //
        // NFR-REL-002: "Redis down → fallback database (không cache), không throw exception" — vì
        // vậy connection string Redis có "abortConnect=false" (đặt ở appsettings.json/docker-compose)
        // để app KHÔNG crash lúc khởi động nếu Redis tạm thời chưa sẵn sàng; health check "redis" ở
        // đây chỉ để BÁO TRẠNG THÁI qua /health/ready, không làm app sập.
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection.");

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
            .AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

        return services;
    }
}
