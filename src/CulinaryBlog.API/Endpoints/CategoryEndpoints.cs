using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories").WithTags("Categories");

        // FR-CAT-001: public, không yêu cầu đăng nhập. Cache 30 phút (NFR-PERF-003) —
        // xem policy "categories" đăng ký trong Program.cs.
        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetCategoriesQuery());
            return result.ToOkResponse();
        }).CacheOutput("categories");
    }
}