using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories").WithTags("Categories");

        group.MapPost("", async (CreateCategoryCommand command, ISender sender) =>
        {
            var category = await sender.Send(command);
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

        group.MapPut("{id:guid}", async (Guid id, UpdateCategoryCommand command, ISender sender) =>
        {
            var category = await sender.Send(command with { Id = id });
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

        group.MapDelete("{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteCategoryCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.Admin);
    }
}