using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
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

        // Soft delete bước cần xóa. Unique (RecipeId, StepNumber) tính cả dòng đã xoá mềm, còn CHECK
        // ("StepNumber" > 0) cấm số âm — nên "đỗ" bước đã xoá ở một số dương lớn, duy nhất trong công
        // thức: lớn hơn mọi số đang có (kể cả các bước đã xoá trước đó) và lớn hơn DeletedBase.
        var maxStepNumber = await steps.Query()
            .IgnoreQueryFilters()
            .Where(s => s.RecipeId == request.RecipeId)
            .MaxAsync(s => (int?)s.StepNumber, ct) ?? 0;

        step.IsDeleted = true;
        step.StepNumber = Math.Max(maxStepNumber, RecipeStepNumbering.DeletedBase) + 1;
        steps.Update(step);

        var remainingSteps = await steps.Query()
            .Where(s => s.RecipeId == request.RecipeId && s.Id != request.StepId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(ct);

        if (remainingSteps.Count > 0)
        {
            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Bước 1: gán số tạm (dương, ngoài dải số thật)
                for (int i = 0; i < remainingSteps.Count; i++)
                {
                    remainingSteps[i].StepNumber = RecipeStepNumbering.TempBase + i;
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
