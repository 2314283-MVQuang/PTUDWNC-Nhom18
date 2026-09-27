using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
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
    }
}