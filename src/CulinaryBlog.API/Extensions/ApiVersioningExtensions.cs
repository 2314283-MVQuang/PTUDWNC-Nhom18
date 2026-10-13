using Asp.Versioning;

namespace CulinaryBlog.API.Extensions;

public static class ApiVersioningExtensions
{
    public static IServiceCollection AddCulinaryApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
        return services;
    }

    public static RouteGroupBuilder MapV1Group(this IEndpointRouteBuilder app, string path)
    {
        var versions = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).ReportApiVersions().Build();
        return app.MapGroup("/api/v{version:apiVersion}" + path)
            .WithApiVersionSet(versions).MapToApiVersion(new ApiVersion(1, 0)).WithGroupName("v1");
    }
}
