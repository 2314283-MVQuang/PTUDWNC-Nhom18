using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.API.Endpoints;

public record UpdateCategoryRequest(
    string Name,
    string? Description = null,
    string? ImageUrl = null);

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories").WithTags("Categories");

        // FR-CAT-001: public, không yêu cầu đăng nhập. Cache 30 phút (NFR-PERF-003) —
        // xem policy "categories" (có tag "categories") đăng ký trong Program.cs.
        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetCategoriesQuery());
            return result.ToOkResponse();
        }).CacheOutput("categories");

        // Xem 1 danh mục theo ID (phục vụ màn hình sửa danh mục ở dashboard Admin).
        group.MapGet("/{id:guid}", async (Guid id, IRepository<Category> categories, CancellationToken ct) =>
        {
            var category = await categories.Query()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category is null)
            {
                return Results.NotFound(new { message = $"Không tìm thấy danh mục với ID '{id}'." });
            }

            var response = new
            {
                category.Id,
                category.Name,
                category.Slug,
                category.Description,
                category.ImageUrl,
                category.OrderIndex
            };

            return Results.Ok(new { data = response });
        });

        // FR-CAT-003: Tạo danh mục mới (Admin). Xoá cache danh sách ngay sau khi tạo (mâu thuẫn #4).
        group.MapPost("", async (CreateCategoryCommand command, ISender sender, IOutputCacheStore cacheStore, CancellationToken ct) =>
        {
            var category = await sender.Send(command, ct);
            await cacheStore.EvictByTagAsync("categories", ct);

            var response = new
            {
                category.Id,
                category.Name,
                category.Slug,
                category.Description,
                category.ImageUrl,
            };

            // SRS FR-CAT-003: Location header trỏ đến /api/v1/categories/{newSlug}.
            return Results.Created($"/api/v1/categories/{category.Slug}", new { data = response });
        }).RequireAuthorization(AuthorizationPolicies.Admin);

        // FR-CAT-004: Cập nhật danh mục (Admin). Handler tự xoá cache tag "categories".
        group.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateCategoryCommand(id, request.Name, request.Description, request.ImageUrl);
            var category = await sender.Send(command, ct);

            var response = new
            {
                category.Id,
                category.Name,
                category.Slug,
                category.Description,
                category.ImageUrl,
            };

            return Results.Ok(new { data = response });
        }).RequireAuthorization(AuthorizationPolicies.Admin);
    }
}
