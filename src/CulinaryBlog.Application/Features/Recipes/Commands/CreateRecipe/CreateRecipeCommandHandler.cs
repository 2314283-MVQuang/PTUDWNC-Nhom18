using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;

/// <summary>
/// FR-RCP-003.
/// Flow:
/// JWT -> AuthorId
/// -> kiểm tra Category
/// -> sinh slug unique
/// -> tạo Recipe + Steps + Ingredients
/// -> SaveChanges
/// -> query lại detail
/// </summary>
public sealed class CreateRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    IRepository<Category> categoryRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateRecipeCommand, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(
        CreateRecipeCommand request,
        CancellationToken ct)
    {
        // Không tin AuthorId từ client.
        // AuthorId phải lấy từ JWT.
        if (!currentUser.IsAuthenticated ||
            string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new UnauthorizedException(
                "Bạn cần đăng nhập để tạo công thức.");
        }

        // Category phải tồn tại.
        var category = await categoryRepository
            .GetByIdAsync(request.CategoryId, ct);

        if (category is null)
        {
            throw new NotFoundException(
                nameof(Category),
                request.CategoryId);
        }

        // Sinh slug:
        // pho-bo
        // pho-bo-2
        // pho-bo-3
        var slug = await BuildUniqueSlugAsync(
            request.Title,
            ct);

        var recipe = new Recipe
        {
            Title = request.Title.Trim(),
            Slug = slug,
            Description = request.Description.Trim(),
            Instructions = request.Instructions.Trim(),

            PrepTime = request.PrepTime,
            CookTime = request.CookTime,
            Servings = request.Servings,

            Difficulty = request.Difficulty,

            // Recipe mới luôn Draft.
            Status = RecipeStatus.Draft,

            CategoryId = category.Id,
            AuthorId = currentUser.UserId,

            Nutrition = new RecipeNutrition
            {
                Calories = request.Nutrition?.Calories,
                Protein = request.Nutrition?.Protein,
                Carbohydrates = request.Nutrition?.Carbohydrates,
                Fat = request.Nutrition?.Fat,
                Fiber = request.Nutrition?.Fiber,
                Sodium = request.Nutrition?.Sodium,
            },
        };

        // Tạo Steps cùng lúc với Recipe.
        if (request.Steps is not null)
        {
            foreach (var step in request.Steps
                         .OrderBy(x => x.StepNumber))
            {
                recipe.Steps.Add(new RecipeStep
                {
                    StepNumber = step.StepNumber,
                    Title = step.Title.Trim(),
                    Description = step.Description.Trim(),
                    TimerMinutes = step.TimerMinutes,
                    ImageUrl = NullIfBlank(step.ImageUrl),
                });
            }
        }

        // Tạo Ingredients cùng lúc với Recipe.
        if (request.Ingredients is not null)
        {
            foreach (var ingredient in request.Ingredients
                         .OrderBy(x => x.OrderIndex))
            {
                recipe.Ingredients.Add(
                    new RecipeIngredient
                    {
                        Name = ingredient.Name.Trim(),
                        Quantity = ingredient.Quantity,
                        Unit = NullIfBlank(ingredient.Unit),
                        Notes = NullIfBlank(ingredient.Notes),
                        OrderIndex = ingredient.OrderIndex,
                    });
            }
        }

        await recipeRepository.AddAsync(recipe, ct);

        // Một SaveChanges cho toàn bộ Recipe aggregate.
        await unitOfWork.SaveChangesAsync(ct);

        // Query lại:
        // 1. lấy Category/Author navigation
        // 2. lấy RowVersion do DB sinh
        // 3. đảm bảo response giống GET detail
        var createdRecipe =
            await recipeRepository.GetByIdWithDetailsAsync(
                recipe.Id,
                ct)
            ?? throw new RecipeNotFoundException(recipe.Id);

        return createdRecipe.ToDetailDto();
    }

    private async Task<string> BuildUniqueSlugAsync(
        string title,
        CancellationToken ct)
    {
        var baseSlug = SlugHelper.GenerateSlug(title);

        // Trường hợp bình thường.
        if (!await recipeRepository.SlugExistsAsync(
                baseSlug,
                null,
                ct))
        {
            return baseSlug;
        }

        // Nếu trùng:
        // pho-bo
        // pho-bo-2
        // pho-bo-3
        // ...
        for (var suffix = 2;
             suffix <= 10_000;
             suffix++)
        {
            var candidate =
                SlugHelper.AppendSuffix(
                    baseSlug,
                    suffix);

            if (!await recipeRepository.SlugExistsAsync(
                    candidate,
                    null,
                    ct))
            {
                return candidate;
            }
        }

        throw new ConflictException(
            "Không thể tạo slug duy nhất cho công thức.");
    }

    private static string? NullIfBlank(
        string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length == 0
            ? null
            : trimmed;
    }
}