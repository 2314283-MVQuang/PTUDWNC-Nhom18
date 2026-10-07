using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeImages;

public record GetRecipeImagesQuery(Guid RecipeId) : IRequest<List<RecipeImageDto>>;

public class GetRecipeImagesQueryHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeImage> images)
    : IRequestHandler<GetRecipeImagesQuery, List<RecipeImageDto>>
{
    public async Task<List<RecipeImageDto>> Handle(GetRecipeImagesQuery request, CancellationToken ct)
    {
        var recipeExists = await recipes.Query()
            .AnyAsync(r => r.Id == request.RecipeId, ct);

        if (!recipeExists)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        var list = await images.Query()
            .Where(i => i.RecipeId == request.RecipeId)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.OrderIndex)
            .Select(i => new RecipeImageDto(
                i.Id,
                i.RecipeId,
                i.OriginalUrl,
                i.MediumUrl,
                i.ThumbnailUrl,
                i.AltText,
                i.IsPrimary,
                i.OrderIndex))
            .ToListAsync(ct);

        return list;
    }
}
