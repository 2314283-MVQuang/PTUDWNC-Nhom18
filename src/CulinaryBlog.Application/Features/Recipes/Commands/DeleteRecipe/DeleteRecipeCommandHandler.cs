using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe;

/// <summary>
/// FR-RCP-007 (mâu thuẫn #1): xoá mềm — chỉ đánh dấu <c>IsDeleted</c> và ghi <c>DeletedAt</c>; công
/// thức biến mất khỏi mọi truy vấn nhờ Global Query Filter nhưng vẫn còn trong DB.
/// <c>PurgeDeletedRecipesJob</c> (FR-JOB-004) xoá hẳn sau 30 ngày.
/// </summary>
public class DeleteRecipeCommandHandler(
    IRecipeRepository recipes,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator)
    : IRequestHandler<DeleteRecipeCommand>
{
    public async Task Handle(DeleteRecipeCommand request, CancellationToken ct)
    {
        var recipe = await recipes.GetByIdAsync(request.Id, ct)
            ?? throw new RecipeNotFoundException(request.Id);

        // Chỉ tác giả sở hữu hoặc Admin (403) — dùng chung quy tắc với sửa/lưu trữ công thức.
        RecipeAuthorization.EnsureCanModify(recipe, currentUser);

        recipe.IsDeleted = true;
        recipe.DeletedAt = DateTimeOffset.UtcNow;

        await unitOfWork.SaveChangesAsync(ct);

        // Công thức đã xoá không được còn trong bất kỳ bản cache công khai nào (mâu thuẫn #4).
        await cacheInvalidator.EvictByTagAsync("recipes", ct);
        await cacheInvalidator.EvictByTagAsync($"recipe-{recipe.Id}", ct);
        await cacheInvalidator.EvictByTagAsync("search", ct);
        await cacheInvalidator.EvictByTagAsync("categories", ct);
    }
}
