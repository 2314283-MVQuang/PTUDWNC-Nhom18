using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeById;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeBySlug;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;
using CulinaryBlog.Domain.Enums;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Recipe endpoints cho Tuần 3.
/// Endpoint không tự try/catch.
/// Exception đi qua GlobalExceptionMiddleware.
/// </summary>
public static class RecipesEndpoints
{
    public static void MapRecipeEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/v1/recipes")
                .WithTags("Recipes");

        // -----------------------------------------------------------
        // POST /api/v1/recipes
        // -----------------------------------------------------------
        group.MapPost(
            "",
            async (
                CreateRecipeCommand command,
                ISender sender) =>
            {
                var result =
                    await sender.Send(command);

                return result.ToCreatedResponse(
                    $"/api/v1/recipes/{result.Slug}");
            })
            .RequireAuthorization(
                AuthorizationPolicies.Author);

        // -----------------------------------------------------------
        // GET /api/v1/recipes
        // -----------------------------------------------------------
        group.MapGet(
            "",
            async (
                int? page,
                int? pageSize,
                Guid? categoryId,
                RecipeDifficulty? difficulty,
                int? maxCookTime,
                int? minServings,
                string? sort,
                ISender sender) =>
            {
                var query =
                    new GetRecipesQuery(
                        Page: page ?? 1,
                        PageSize: pageSize ?? 12,
                        CategoryId: categoryId,
                        Difficulty: difficulty,
                        MaxCookTime: maxCookTime,
                        MinServings: minServings,
                        Sort: string.IsNullOrWhiteSpace(sort)
                            ? "-createdAt"
                            : sort);

                var result =
                    await sender.Send(query);

                return result.ToPagedResponse();
            });

        // -----------------------------------------------------------
        // GET /api/v1/recipes/{id}
        // -----------------------------------------------------------
        group.MapGet(
            "/{id:guid}",
            async (
                Guid id,
                ISender sender) =>
            {
                var result =
                    await sender.Send(
                        new GetRecipeByIdQuery(id));

                return result.ToOkResponse();
            });

        // -----------------------------------------------------------
        // GET /api/v1/recipes/{slug}
        // -----------------------------------------------------------
        // Thêm để frontend hiện tại sử dụng /recipes/{slug}.
        group.MapGet(
            "/{slug}",
            async (
                string slug,
                ISender sender) =>
            {
                var result =
                    await sender.Send(
                        new GetRecipeBySlugQuery(slug));

                return result.ToOkResponse();
            });
    }
}