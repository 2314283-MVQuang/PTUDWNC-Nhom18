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
        .WithSummary("Search recipes using Vietnamese full-text search")
        .WithDescription("Search recipe titles and descriptions, ignoring Vietnamese diacritics.")
        .Produces(StatusCodes.Status200OK);
    }
}
