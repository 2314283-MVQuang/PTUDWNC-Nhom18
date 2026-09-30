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
        // --- Module-specific repositories ---
        services.AddScoped<ICategoryRepository, CategoryRepository>();
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

        // --- Email: FR-AUTH-001 (Register) gửi email chào mừng qua service này ---
        // IFileStorageService đã gỡ cùng module Recipe (upload ảnh công thức, không thuộc FR-AUTH).
        services.AddScoped<IEmailService, ConsoleEmailService>();

        return services;
    }
}
