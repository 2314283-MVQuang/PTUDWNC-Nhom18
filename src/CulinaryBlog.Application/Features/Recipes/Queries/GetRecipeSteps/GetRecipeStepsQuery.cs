using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeSteps;

public record GetRecipeStepsQuery(Guid RecipeId) : IRequest<List<RecipeStepItemDto>>;

public class GetRecipeStepsQueryHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeStep> steps,
    ICurrentUser currentUser)
    : IRequestHandler<GetRecipeStepsQuery, List<RecipeStepItemDto>>
{
    public async Task<List<RecipeStepItemDto>> Handle(GetRecipeStepsQuery request, CancellationToken ct)
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

        var list = await steps.Query()
            .Where(s => s.RecipeId == request.RecipeId)
            .OrderBy(s => s.StepNumber)
            .Select(s => new RecipeStepItemDto(
                s.Id,
                s.RecipeId,
                s.StepNumber,
                s.Title,
                s.Description,
                s.TimerMinutes,
                s.ImageUrl))
            .ToListAsync(ct);

        return list;
    }
}
