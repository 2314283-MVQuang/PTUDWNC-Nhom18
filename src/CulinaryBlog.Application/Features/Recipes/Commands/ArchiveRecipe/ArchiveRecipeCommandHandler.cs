using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.ArchiveRecipe;

/// <summary>
/// FR-RCP-006. Luồng: tìm recipe (404 nếu không có) → kiểm tra quyền theo resource: chỉ chủ sở hữu
/// hoặc Admin (403) → gọi domain method Archive()/Unarchive() → lưu → xoá cache → trả RecipeDetailDto.
///
/// Recipe "Archived" tự động biến mất khỏi phía công khai mà không cần sửa thêm query nào:
///   - GetRecipesQueryHandler: khách chỉ thấy Published; tác giả vẫn thấy bài của mình ("công thức
///     của tôi"), Admin thấy tất cả.
///   - RecipeRepository.SearchRecipesAsync: chỉ trả Published.
///   - GetRecipeById/BySlug: Draft/Archived chỉ chủ sở hữu và Admin xem được.
///   - GetCategoriesQueryHandler: RecipeCount chỉ đếm Published.
/// </summary>
public sealed class ArchiveRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator)
    : IRequestHandler<ArchiveRecipeCommand, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(
        ArchiveRecipeCommand request,
        CancellationToken ct)
    {
        var recipe =
            await recipeRepository.GetByIdAsync(
                request.Id,
                ct);

        if (recipe is null)
        {
            throw new RecipeNotFoundException(request.Id);
        }

        RecipeAuthorization.EnsureCanModify(
            recipe,
            currentUser);

        var statusBefore = recipe.Status;

        if (request.Archive)
        {
            recipe.Archive();
        }
        else
        {
            recipe.Unarchive();
        }

        // Idempotent: trạng thái không đổi thì không ghi DB và không xoá cache.
        if (recipe.Status != statusBefore)
        {
            await unitOfWork.SaveChangesAsync(ct);

            // Mâu thuẫn #4: evict theo event. Danh sách/chi tiết công thức ("recipes", "recipe-{id}"),
            // kết quả tìm kiếm ("search") và số công thức mỗi danh mục ("categories") đều phụ thuộc
            // trạng thái Published nên đều phải làm mới.
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
            await cacheInvalidator.EvictByTagAsync($"recipe-{recipe.Id}", ct);
            await cacheInvalidator.EvictByTagAsync("search", ct);
            await cacheInvalidator.EvictByTagAsync("categories", ct);
        }

        // Đọc lại kèm Category/Author/Steps... và RowVersion mới do PostgreSQL sinh.
        var updated =
            await recipeRepository.GetByIdWithDetailsAsync(
                recipe.Id,
                ct);

        if (updated is null)
        {
            throw new RecipeNotFoundException(recipe.Id);
        }

        return updated.ToDetailDto();
    }
}
