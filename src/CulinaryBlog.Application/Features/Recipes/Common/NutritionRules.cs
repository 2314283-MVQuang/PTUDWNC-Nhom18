using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Common;

/// <summary>
/// Ràng buộc chung cho 6 chỉ số dinh dưỡng (RecipeNutrition, SRS mục 7.2.1): tuỳ chọn,
/// nếu có thì từ 0 đến 99 999 — vừa chặn số âm, vừa không tràn cột decimal(8,2).
/// </summary>
public static class NutritionRules
{
    public const decimal Max = 99999m;

    public static IRuleBuilderOptions<T, decimal?> ValidNutrient<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.InclusiveBetween(0m, Max).WithMessage("{PropertyName} phải từ 0 đến 99999.");
}
