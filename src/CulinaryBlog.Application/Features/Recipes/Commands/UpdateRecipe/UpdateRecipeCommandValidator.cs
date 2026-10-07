using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe;

public sealed class UpdateRecipeCommandValidator
    : AbstractValidator<UpdateRecipeCommand>
{
    public UpdateRecipeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("RecipeId không được để trống.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200)
            .When(x => x.Title is not null)
            .WithMessage("Title phải từ 1 đến 200 ký tự.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2000)
            .When(x => x.Description is not null)
            .WithMessage("Description phải từ 1 đến 2000 ký tự.");

        RuleFor(x => x.CategoryId)
            .NotEmpty()
            .When(x => x.CategoryId.HasValue)
            .WithMessage("CategoryId không hợp lệ.");

        RuleFor(x => x.PrepTime)
            .GreaterThan(0)
            .When(x => x.PrepTime.HasValue)
            .WithMessage("PrepTime phải lớn hơn 0.");

        RuleFor(x => x.CookTime)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CookTime.HasValue)
            .WithMessage("CookTime không được âm.");

        RuleFor(x => x.Servings)
            .GreaterThan(0)
            .When(x => x.Servings.HasValue)
            .WithMessage("Servings phải lớn hơn 0.");

        RuleFor(x => x.Difficulty)
            .IsInEnum()
            .When(x => x.Difficulty.HasValue)
            .WithMessage("Difficulty không hợp lệ.");

        RuleFor(x => x.Instructions)
            .MaximumLength(10000)
            .When(x => x.Instructions is not null)
            .WithMessage("Instructions tối đa 10000 ký tự.");

        RuleFor(x => x.IfMatch)
            .NotEmpty()
            .WithMessage("If-Match là bắt buộc.");
    }
}
