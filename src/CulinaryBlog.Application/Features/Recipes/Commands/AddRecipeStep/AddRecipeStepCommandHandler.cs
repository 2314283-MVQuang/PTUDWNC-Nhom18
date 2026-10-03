using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeStep;

public class AddRecipeStepCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeStep> steps,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<AddRecipeStepCommand, RecipeStepDto>
{
    public async Task<RecipeStepDto> Handle(AddRecipeStepCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền thêm bước thực hiện cho công thức này.");
        }

        var existingSteps = await steps.Query()
            .Where(s => s.RecipeId == request.RecipeId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(ct);

        int targetStepNumber;
        if (!request.StepNumber.HasValue || request.StepNumber.Value > existingSteps.Count + 1)
        {
            targetStepNumber = existingSteps.Count + 1;
        }
        else
        {
            targetStepNumber = request.StepNumber.Value;
        }

        var newStep = new RecipeStep
        {
            Id = Guid.NewGuid(),
            RecipeId = request.RecipeId,
            StepNumber = targetStepNumber,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            TimerMinutes = request.TimerMinutes,
            ImageUrl = NullIfBlank(request.ImageUrl)
        };

        var stepsToShift = existingSteps.Where(s => s.StepNumber >= targetStepNumber).ToList();

        if (stepsToShift.Count > 0)
        {
            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Bước 1: gán số âm tạm thời để không đụng unique index (RecipeId, StepNumber)
                for (int i = 0; i < stepsToShift.Count; i++)
                {
                    stepsToShift[i].StepNumber = -(i + 1000);
                }
                await unitOfWork.SaveChangesAsync(ct);

                // Bước 2: gán số chính thức đã dịch chuyển
                for (int i = 0; i < stepsToShift.Count; i++)
                {
                    stepsToShift[i].StepNumber = targetStepNumber + 1 + i;
                }
                await steps.AddAsync(newStep, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }, ct);
        }
        else
        {
            await steps.AddAsync(newStep, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{request.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }

        return new RecipeStepDto(
            newStep.Id,
            newStep.RecipeId,
            newStep.StepNumber,
            newStep.Title,
            newStep.Description,
            newStep.TimerMinutes,
            newStep.ImageUrl);
    }

    private static string? NullIfBlank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
