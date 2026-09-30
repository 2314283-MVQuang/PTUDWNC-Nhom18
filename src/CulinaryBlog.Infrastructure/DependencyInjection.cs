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
        // --- Database (CONS-006: PostgreSQL chính, tự động fallback SQLite khi chạy local chưa bật Docker) ---
        var pgConnection = configuration.GetConnectionString("DefaultConnection");
        bool isPgAvailable = false;
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            var result = tcp.BeginConnect("localhost", 5432, null, null);
            isPgAvailable = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
            if (isPgAvailable) tcp.EndConnect(result);
        }
        catch
        {
            isPgAvailable = false;
        }

        services.AddDbContext<CulinaryBlogDbContext>(options =>
        {
            if (isPgAvailable && !string.IsNullOrWhiteSpace(pgConnection))
            {
                options.UseNpgsql(pgConnection);
            }
            else
            {
                var dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "culinaryblog.db");
                options.UseSqlite($"Data Source={dbPath}");
            }
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CulinaryBlogDbContext>());

        // --- Repositories ---
        // CHỈ giữ lại những gì FR-AUTH cần. ICategoryRepository/IRecipeRepository đã gỡ cùng module
        // Category/Recipe (xem ghi chú trong Program.cs) — sẽ đăng ký lại khi nhóm triển khai tiếp.
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
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

        // --- Email: FR-AUTH-001 (Register) gửi email chào mừng qua service này; Tuần 3 thêm
        // reset mật khẩu + xác nhận email (cùng interface IEmailService) ---
        services.AddScoped<IEmailService, ConsoleEmailService>();

        // --- UploadedFile Repository & MinIO Storage (FR-FILE-001) ---
        services.AddScoped<IUploadedFileRepository, UploadedFileRepository>();
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        // --- Health checks (FR-OBS-001, Tuần 3) ---
        // "postgresql" gắn tag "ready" — dùng cho /health/ready (app đã sẵn sàng nhận traffic
        // chưa, CÓ kiểm tra dependency ngoài). /health/live KHÔNG chạy check nào (xem
        // API/Endpoints/HealthEndpoints.cs) nên không cần đăng ký gì thêm ở đây cho liveness.
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection.");

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);

        return services;
    }
}
