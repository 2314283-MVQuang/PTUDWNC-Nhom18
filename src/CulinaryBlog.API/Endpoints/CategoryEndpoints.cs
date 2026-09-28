using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
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

        // FR-CAT-001: Xem danh sách danh mục (cache tag: categories)
        group.MapGet("", async (IRepository<Category> categories, CancellationToken ct) =>
        {
            var list = await categories.Query()
                .OrderBy(c => c.OrderIndex)
                .ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Slug,
                    c.Description,
                    c.ImageUrl,
                    c.OrderIndex
                })
                .ToListAsync(ct);

            return Results.Ok(new { data = list });
        }).CacheOutput(p => p.Tag("categories").Expire(TimeSpan.FromMinutes(30)));

        // FR-CAT-002: Xem chi tiết danh mục
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

        // FR-CAT-003: Tạo danh mục mới (Admin)
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

            return Results.Created($"/api/v1/categories/{category.Id}", new { data = response });
        }).RequireAuthorization(AuthorizationPolicies.Admin);

        // FR-CAT-004: Cập nhật danh mục (Admin) - MT-06 slug auto-suffix
        group.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest request, ISender sender, IOutputCacheStore cacheStore, CancellationToken ct) =>
        {
            var command = new UpdateCategoryCommand(id, request.Name, request.Description, request.ImageUrl);
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

            return Results.Ok(new { data = response });
        }).RequireAuthorization(AuthorizationPolicies.Admin);
    }
}