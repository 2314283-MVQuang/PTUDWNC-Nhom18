using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

public sealed class SearchRecipesQueryValidator : AbstractValidator<SearchRecipesQuery>
{
    public SearchRecipesQueryValidator()
    {
        RuleFor(x => x.CategoryId).NotEqual(Guid.Empty)
            .WithMessage("Danh mục không hợp lệ.");
        RuleFor(x => x.Difficulty).IsInEnum()
            .WithMessage("Độ khó phải là Easy, Medium, Hard hoặc Expert.");
        RuleFor(x => x.MinPrepTime).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxPrepTime).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinCookTime).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxCookTime).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxPrepTime).GreaterThanOrEqualTo(x => x.MinPrepTime)
            .When(x => x.MinPrepTime.HasValue && x.MaxPrepTime.HasValue)
            .WithMessage("Thời gian chuẩn bị tối đa phải lớn hơn hoặc bằng tối thiểu.");
        RuleFor(x => x.MaxCookTime).GreaterThanOrEqualTo(x => x.MinCookTime)
            .When(x => x.MinCookTime.HasValue && x.MaxCookTime.HasValue)
            .WithMessage("Thời gian nấu tối đa phải lớn hơn hoặc bằng tối thiểu.");
    }
}
