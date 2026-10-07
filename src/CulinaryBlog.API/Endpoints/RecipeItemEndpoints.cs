using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeImage;
using CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeIngredient;
using CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeStep;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeImage;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeIngredient;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeStep;
using CulinaryBlog.Application.Features.Recipes.Commands.SetPrimaryRecipeImage;
using CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeIngredient;
using CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeStep;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeImages;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeIngredients;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeSteps;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

public record AddIngredientRequest(
    string Name,
    decimal? Quantity = null,
    string? Unit = null,
    string? Notes = null,
    int? OrderIndex = null);

public record UpdateIngredientRequest(
    string Name,
    decimal? Quantity = null,
    string? Unit = null,
    string? Notes = null,
    int? OrderIndex = null);

public record AddStepRequest(
    string Title,
    string Description,
    int? StepNumber = null,
    int? TimerMinutes = null,
    string? ImageUrl = null);

public record UpdateStepRequest(
    string Title,
    string Description,
    int? StepNumber = null,
    int? TimerMinutes = null,
    string? ImageUrl = null);

public record AddRecipeImageRequest(
    string ImageUrl,
    string? AltText = null,
    bool IsPrimary = false,
    int? OrderIndex = null);

public static class RecipeItemEndpoints
{
    public static void MapRecipeItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recipes/{id:guid}").WithTags("Recipe Items");

        // ==========================================
        // 1. INGREDIENTS (FR-RCP-009)
        // ==========================================

        // GET /api/v1/recipes/{id}/ingredients
        group.MapGet("/ingredients", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRecipeIngredientsQuery(id), ct);
            return Results.Ok(new { data = result });
        });

        // POST /api/v1/recipes/{id}/ingredients
        group.MapPost("/ingredients", async (Guid id, AddIngredientRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AddRecipeIngredientCommand(
                id,
                request.Name,
                request.Quantity,
                request.Unit,
                request.Notes,
                request.OrderIndex);

            var result = await sender.Send(command, ct);
            return Results.Created($"/api/v1/recipes/{id}/ingredients/{result.Id}", new { data = result });
        }).RequireAuthorization(AuthorizationPolicies.Author);

        // PUT /api/v1/recipes/{id}/ingredients/{ingredientId}
        group.MapPut("/ingredients/{ingredientId:guid}", async (Guid id, Guid ingredientId, UpdateIngredientRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateRecipeIngredientCommand(
                id,
                ingredientId,
                request.Name,
                request.Quantity,
                request.Unit,
                request.Notes,
                request.OrderIndex);

            var result = await sender.Send(command, ct);
            return Results.Ok(new { data = result });
        }).RequireAuthorization(AuthorizationPolicies.Author);

        // DELETE /api/v1/recipes/{id}/ingredients/{ingredientId}
        group.MapDelete("/ingredients/{ingredientId:guid}", async (Guid id, Guid ingredientId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteRecipeIngredientCommand(id, ingredientId), ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.Author);


        // ==========================================
        // 2. STEPS (FR-RCP-010)
        // ==========================================

        // GET /api/v1/recipes/{id}/steps
        group.MapGet("/steps", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRecipeStepsQuery(id), ct);
            return Results.Ok(new { data = result });
        });

        // POST /api/v1/recipes/{id}/steps
        group.MapPost("/steps", async (Guid id, AddStepRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AddRecipeStepCommand(
                id,
                request.Title,
                request.Description,
                request.StepNumber,
                request.TimerMinutes,
                request.ImageUrl);

            var result = await sender.Send(command, ct);
            return Results.Created($"/api/v1/recipes/{id}/steps/{result.Id}", new { data = result });
        }).RequireAuthorization(AuthorizationPolicies.Author);

        // PUT /api/v1/recipes/{id}/steps/{stepId}
        group.MapPut("/steps/{stepId:guid}", async (Guid id, Guid stepId, UpdateStepRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateRecipeStepCommand(
                id,
                stepId,
                request.Title,
                request.Description,
                request.StepNumber,
                request.TimerMinutes,
                request.ImageUrl);

            var result = await sender.Send(command, ct);
            return Results.Ok(new { data = result });
        }).RequireAuthorization(AuthorizationPolicies.Author);

        // DELETE /api/v1/recipes/{id}/steps/{stepId}
        group.MapDelete("/steps/{stepId:guid}", async (Guid id, Guid stepId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteRecipeStepCommand(id, stepId), ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.Author);


        // ==========================================
        // 3. IMAGES (FR-RCP-008 & MinIO Presigned URL)
        // ==========================================

        // GET /api/v1/recipes/{id}/images
        group.MapGet("/images", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRecipeImagesQuery(id), ct);
            return Results.Ok(new { data = result });
        });

        // POST /api/v1/recipes/{id}/images
        group.MapPost("/images", async (Guid id, AddRecipeImageRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AddRecipeImageCommand(
                id,
                request.ImageUrl,
                request.AltText,
                request.IsPrimary,
                request.OrderIndex);

            var result = await sender.Send(command, ct);
            return Results.Created($"/api/v1/recipes/{id}/images/{result.Id}", new { data = result });
        }).RequireAuthorization(AuthorizationPolicies.Author);

        // PATCH /api/v1/recipes/{id}/images/{imageId}/primary
        group.MapPatch("/images/{imageId:guid}/primary", async (Guid id, Guid imageId, ISender sender, CancellationToken ct) =>
        {
            var command = new SetPrimaryRecipeImageCommand(id, imageId);
            var result = await sender.Send(command, ct);
            return Results.Ok(new { data = result });
        }).RequireAuthorization(AuthorizationPolicies.Author);

        // DELETE /api/v1/recipes/{id}/images/{imageId}
        group.MapDelete("/images/{imageId:guid}", async (Guid id, Guid imageId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteRecipeImageCommand(id, imageId), ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.Author);
    }
}
