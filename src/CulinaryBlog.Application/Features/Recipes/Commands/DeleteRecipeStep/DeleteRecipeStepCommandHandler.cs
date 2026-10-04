using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeStep;

public class DeleteRecipeStepCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeStep> steps,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<DeleteRecipeStepCommand>
{
    public async Task Handle(DeleteRecipeStepCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền xóa bước thực hiện của công thức này.");
        }

        var step = await steps.Query()
            .FirstOrDefaultAsync(s => s.Id == request.StepId && s.RecipeId == request.RecipeId, ct);

        if (step is null)
        {
            throw new NotFoundException(nameof(RecipeStep), request.StepId);
        }

        // Soft delete bước cần xóa: gán số âm duy nhất để không đụng unique index (RecipeId, StepNumber)
        step.IsDeleted = true;
        step.StepNumber = -Math.Abs(step.Id.GetHashCode());
        if (step.StepNumber == 0) step.StepNumber = -10000;
        steps.Update(step);

        var remainingSteps = await steps.Query()
            .Where(s => s.RecipeId == request.RecipeId && s.Id != request.StepId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(ct);

        if (remainingSteps.Count > 0)
        {
            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Bước 1: gán số âm tạm thời
                for (int i = 0; i < remainingSteps.Count; i++)
                {
                    remainingSteps[i].StepNumber = -(i + 1000);
                }
                await unitOfWork.SaveChangesAsync(ct);

                // Bước 2: gán số chuẩn liên tục 1, 2, 3...
                for (int i = 0; i < remainingSteps.Count; i++)
                {
                    remainingSteps[i].StepNumber = i + 1;
                }
                await unitOfWork.SaveChangesAsync(ct);
            }, ct);
        }
        else
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{request.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }
    }
}
