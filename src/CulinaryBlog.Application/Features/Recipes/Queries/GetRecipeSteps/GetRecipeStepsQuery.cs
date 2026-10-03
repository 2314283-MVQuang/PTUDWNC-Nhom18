using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeSteps;

public record GetRecipeStepsQuery(Guid RecipeId) : IRequest<List<RecipeStepDto>>;

public class GetRecipeStepsQueryHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeStep> steps)
    : IRequestHandler<GetRecipeStepsQuery, List<RecipeStepDto>>
{
    public async Task<List<RecipeStepDto>> Handle(GetRecipeStepsQuery request, CancellationToken ct)
    {
        var recipeExists = await recipes.Query()
            .AnyAsync(r => r.Id == request.RecipeId, ct);

        if (!recipeExists)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        var list = await steps.Query()
            .Where(s => s.RecipeId == request.RecipeId)
            .OrderBy(s => s.StepNumber)
            .Select(s => new RecipeStepDto(
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
