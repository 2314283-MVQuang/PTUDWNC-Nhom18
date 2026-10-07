using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe;
using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recipes").WithTags("Recipes");

        group.MapGet("/search", async (
            [FromQuery(Name = "q")] string q,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var recipes = await sender.Send(new SearchRecipesQuery(q), cancellationToken);
            return Results.Ok(recipes);
        })
        .WithName("SearchRecipes")
        .WithSummary("Full-text Search Recipes")
        .WithDescription("Search recipe titles and descriptions, ignoring Vietnamese diacritics.")
        .Produces(StatusCodes.Status200OK);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteRecipeCommand(id), cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("DeleteRecipe")
        .WithSummary("Soft-delete a recipe")
        .WithDescription("Marks a recipe as deleted. It is physically purged after 30 days.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthorizationPolicies.Author);
    }
}
