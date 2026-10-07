using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeStep;

public class UpdateRecipeStepCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeStep> steps,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<UpdateRecipeStepCommand, RecipeStepItemDto>
{
    public async Task<RecipeStepItemDto> Handle(UpdateRecipeStepCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền chỉnh sửa bước thực hiện của công thức này.");
        }

        var step = await steps.Query()
            .FirstOrDefaultAsync(s => s.Id == request.StepId && s.RecipeId == request.RecipeId, ct);

        if (step is null)
        {
            throw new NotFoundException(nameof(RecipeStep), request.StepId);
        }

        step.Title = request.Title.Trim();
        step.Description = request.Description.Trim();
        step.TimerMinutes = request.TimerMinutes;
        step.ImageUrl = NullIfBlank(request.ImageUrl);

        if (request.StepNumber.HasValue && request.StepNumber.Value != step.StepNumber)
        {
            var targetNum = request.StepNumber.Value;
            var allSteps = await steps.Query()
                .Where(s => s.RecipeId == request.RecipeId)
                .OrderBy(s => s.StepNumber)
                .ToListAsync(ct);

            // Xóa step này ra khỏi danh sách tạm rồi chèn vào vị trí targetNum
            allSteps.Remove(step);
            var insertIndex = Math.Clamp(targetNum - 1, 0, allSteps.Count);
            allSteps.Insert(insertIndex, step);

            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Bước 1: đánh số âm tạm thời
                for (int i = 0; i < allSteps.Count; i++)
                {
                    allSteps[i].StepNumber = -(i + 1000);
                }
                await unitOfWork.SaveChangesAsync(ct);

                // Bước 2: gán số chuẩn 1, 2, 3...
                for (int i = 0; i < allSteps.Count; i++)
                {
                    allSteps[i].StepNumber = i + 1;
                }
                await unitOfWork.SaveChangesAsync(ct);
            }, ct);
        }
        else
        {
            steps.Update(step);
            await unitOfWork.SaveChangesAsync(ct);
        }

        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{request.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }

        return new RecipeStepItemDto(
            step.Id,
            step.RecipeId,
            step.StepNumber,
            step.Title,
            step.Description,
            step.TimerMinutes,
            step.ImageUrl);
    }

    private static string? NullIfBlank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
