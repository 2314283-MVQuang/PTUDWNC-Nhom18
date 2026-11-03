using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

/// <summary>
/// FR-SRCH-002 (Ý): bộ lọc; FR-SRCH-004 (Tiến): page >= 1, pageSize 1–50 (sai trả 422).
/// Tham số sort do SortParser kiểm tra riêng (FR-SRCH-003, sai trả 400 INVALID_SORT).
/// </summary>
public sealed class SearchRecipesQueryValidator : AbstractValidator<SearchRecipesQuery>
{
    public SearchRecipesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page phải >= 1.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, SearchRecipesQuery.MaxPageSize)
            .WithMessage($"PageSize phải nằm trong khoảng 1-{SearchRecipesQuery.MaxPageSize}.");
        RuleFor(x => x.CategoryId).NotEqual(Guid.Empty)
            .WithMessage("Danh mục không hợp lệ.");
        RuleFor(x => x.Difficulty).IsInEnum()
            .WithMessage("Độ khó phải là Easy, Medium, Hard hoặc Expert.");
        RuleFor(x => x.MinPrepTime).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxPrepTime).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinCookTime).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxCookTime).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinServings).GreaterThanOrEqualTo(1)
            .WithMessage("Số khẩu phần tối thiểu phải từ 1 trở lên.");
        RuleFor(x => x.MaxPrepTime).GreaterThanOrEqualTo(x => x.MinPrepTime)
            .When(x => x.MinPrepTime.HasValue && x.MaxPrepTime.HasValue)
            .WithMessage("Thời gian chuẩn bị tối đa phải lớn hơn hoặc bằng tối thiểu.");
        RuleFor(x => x.MaxCookTime).GreaterThanOrEqualTo(x => x.MinCookTime)
            .When(x => x.MinCookTime.HasValue && x.MaxCookTime.HasValue)
            .WithMessage("Thời gian nấu tối đa phải lớn hơn hoặc bằng tối thiểu.");
    }
}
