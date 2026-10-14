using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe;

/// <summary>
/// FR-RCP-004:
/// - Owner/Admin authorization
/// - If-Match / RowVersion
/// - Optimistic concurrency
/// - Slug update/collision
/// </summary>
public sealed class UpdateRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    IRepository<Category> categoryRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateRecipeCommand, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(
        UpdateRecipeCommand request,
        CancellationToken ct)
    {
        // =========================================================
        // 1. Đọc RowVersion từ If-Match
        // =========================================================

        var expectedRowVersion =
            ConcurrencyTokenHelper.DecodeIfMatch(
                request.IfMatch);

        // =========================================================
        // 2. Lấy Recipe đang TRACKED
        //
        // Không dùng GetByIdWithDetailsAsync()
        // vì method đó dùng AsNoTracking() để phục vụ GET.
        // =========================================================

        var recipe =
            await recipeRepository.GetByIdAsync(
                request.Id,
                ct);

        if (recipe is null)
        {
            throw new RecipeNotFoundException(
                request.Id);
        }

        // =========================================================
        // 3. Resource-Based Authorization
        //
        // Admin -> sửa được
        // Owner -> sửa được
        // Author khác -> 403
        // =========================================================

        RecipeAuthorization.EnsureCanModify(
            recipe,
            currentUser);

        // =========================================================
        // 4. Kiểm tra RowVersion sớm
        //
        // Nếu client đã giữ phiên bản cũ từ trước thì không cần
        // chạy tiếp business logic.
        // =========================================================

        if (!recipe.RowVersion.SequenceEqual(
                expectedRowVersion))
        {
            throw new RecipeConcurrencyConflictException();
        }

        // =========================================================
        // 5. Nếu đổi Category -> Category mới phải tồn tại
        // =========================================================

        if (request.CategoryId.HasValue &&
            request.CategoryId.Value !=
            recipe.CategoryId)
        {
            var category =
                await categoryRepository.GetByIdAsync(
                    request.CategoryId.Value,
                    ct);

            if (category is null)
            {
                throw new NotFoundException(
                    "Category",
                    request.CategoryId.Value);
            }
        }

        // =========================================================
        // 6. Xác định Title có đổi hay không
        // =========================================================

        var newTitle =
            request.Title?.Trim();

        var titleChanged =
            newTitle is not null &&
            !string.Equals(
                recipe.Title,
                newTitle,
                StringComparison.Ordinal);

        // =========================================================
        // 7. Business rule về Slug
        //
        // Published:
        //   đổi title nhưng giữ slug.
        //
        // Draft / Archived:
        //   đổi title -> sinh slug mới.
        //   nếu trùng -> -2, -3...
        // =========================================================

        if (titleChanged &&
            recipe.Status != RecipeStatus.Published)
        {
            var baseSlug =
                SlugHelper.GenerateSlug(
                    newTitle!);

            var uniqueSlug =
                await BuildUniqueSlugAsync(
                    baseSlug,
                    recipe.Id,
                    ct);

            recipe.ChangeSlug(
                uniqueSlug);
        }

        // =========================================================
        // 8. Update field qua Domain Method
        // =========================================================

        recipe.UpdateBasicInfo(
            title: newTitle,
            description:
                request.Description?.Trim(),
            instructions:
                request.Instructions?.Trim(),
            prepTime:
                request.PrepTime,
            cookTime:
                request.CookTime,
            servings:
                request.Servings,
            difficulty:
                request.Difficulty,
            categoryId:
                request.CategoryId);

        // =========================================================
        // 9. Nutrition nếu có
        // =========================================================

        if (request.Nutrition is not null)
        {
            recipe.UpdateNutrition(
                new RecipeNutrition
                {
                    Calories =
                        request.Nutrition.Calories,

                    Protein =
                        request.Nutrition.Protein,

                    Carbohydrates =
                        request.Nutrition.Carbohydrates,

                    Fat =
                        request.Nutrition.Fat,

                    Fiber =
                        request.Nutrition.Fiber,

                    Sodium =
                        request.Nutrition.Sodium,
                });
        }

        // =========================================================
        // 10. Gắn RowVersion cũ vào EF
        //
        // Đây là phần giúp EF Core biết client đang muốn update
        // phiên bản nào.
        // =========================================================

        recipeRepository.SetOriginalRowVersion(
            recipe,
            expectedRowVersion);

        // =========================================================
        // 11. Save
        // =========================================================

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Có thể xảy ra race-condition:
            //
            // fast-check ở trên vừa đúng,
            // nhưng User B vừa update Recipe
            // ngay trước SaveChanges().
            //
            // EF phát hiện ra -> 409.
            throw new RecipeConcurrencyConflictException();
        }

        // =========================================================
        // 12. Đọc lại Recipe mới nhất
        //
        // Để lấy RowVersion mới do PostgreSQL sinh.
        // =========================================================

        var updatedRecipe =
            await recipeRepository
                .GetByIdWithDetailsAsync(
                    recipe.Id,
                    ct);

        if (updatedRecipe is null)
        {
            throw new RecipeNotFoundException(
                recipe.Id);
        }

        return updatedRecipe.ToDetailDto();
    }

    /// <summary>
    /// Sinh slug duy nhất:
    /// pho-bo
    /// pho-bo-2
    /// pho-bo-3
    /// ...
    /// </summary>
    private async Task<string>
        BuildUniqueSlugAsync(
            string baseSlug,
            Guid recipeId,
            CancellationToken ct)
    {
        // Nếu slug mới chưa được dùng
        // hoặc chỉ chính Recipe này đang dùng,
        // giữ nguyên.
        if (!await recipeRepository.SlugExistsAsync(
                baseSlug,
                recipeId,
                ct))
        {
            return baseSlug;
        }

        for (
            var suffix = 2;
            suffix <= 10_000;
            suffix++)
        {
            var candidate =
                SlugHelper.AppendSuffix(
                    baseSlug,
                    suffix);

            if (!await recipeRepository.SlugExistsAsync(
                    candidate,
                    recipeId,
                    ct))
            {
                return candidate;
            }
        }

        throw new ConflictException(
            "Không thể tạo slug duy nhất cho Recipe.");
    }
}