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
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Repository generic cho các entity con (RecipeStep/RecipeIngredient/RecipeImage): Handler
        // chỉ cần Remove()/AddAsync() một dòng con nên không đáng viết repository chuyên biệt cho
        // từng loại. Đăng ký open generic: xin IRepository<RecipeStep> sẽ nhận RepositoryBase<RecipeStep>.
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

        // --- File storage & Email: xem ghi chú TODO trong từng file, sẽ đổi sang MinIO/SMTP thật sau ---
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IEmailService, ConsoleEmailService>();

        return services;
    }
}
