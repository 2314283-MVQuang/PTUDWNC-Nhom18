using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.Common.Helpers;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.Application;

/// <summary>Điểm đăng ký DI duy nhất của Application Layer — API layer chỉ cần gọi AddApplication().</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        // Thứ tự đăng ký behavior = thứ tự chạy (mục 6.3): Logging trước, Validation sau.
        // TODO (nhóm làm tiếp): thêm CachingBehavior + CacheInvalidationBehavior khi tích hợp Redis.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Tuần 10 (Quang): kiểm tra ảnh trước khi gắn vào công thức / bước thực hiện.
        services.AddScoped<RecipeImageUploadGuard>();

        return services;
    }
}
