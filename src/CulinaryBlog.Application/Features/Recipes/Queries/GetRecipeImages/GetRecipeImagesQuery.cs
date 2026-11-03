using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeImages;

public record GetRecipeImagesQuery(Guid RecipeId) : IRequest<List<RecipeImageItemDto>>;

public class GetRecipeImagesQueryHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeImage> images,
    ICurrentUser currentUser)
    : IRequestHandler<GetRecipeImagesQuery, List<RecipeImageItemDto>>
{
    public async Task<List<RecipeImageItemDto>> Handle(GetRecipeImagesQuery request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .Where(r => r.Id == request.RecipeId)
            .Select(r => new { r.Status, r.AuthorId })
            .FirstOrDefaultAsync(ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        // Bản nháp/lưu trữ: chỉ tác giả hoặc Admin xem được phần con (giống GET /recipes/{id}).
        RecipeAuthorization.EnsureCanRead(recipe.Status, recipe.AuthorId, currentUser);

        var list = await images.Query()
            .Where(i => i.RecipeId == request.RecipeId)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.OrderIndex)
            .Select(i => new RecipeImageItemDto(
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
