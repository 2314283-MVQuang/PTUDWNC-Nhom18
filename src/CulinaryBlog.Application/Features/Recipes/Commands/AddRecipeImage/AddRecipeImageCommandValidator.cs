using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeImage;

public class AddRecipeImageCommandValidator : AbstractValidator<AddRecipeImageCommand>
{
    public AddRecipeImageCommandValidator()
    {
        RuleFor(x => x.RecipeId)
            .NotEmpty().WithMessage("RecipeId không được để trống.");

        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("ImageUrl không được để trống.")
            .MaximumLength(500).WithMessage("Đường dẫn ảnh tối đa 500 ký tự.");

        RuleFor(x => x.AltText)
            .MaximumLength(200).WithMessage("AltText tối đa 200 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.AltText));

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Thứ tự hiển thị phải lớn hơn hoặc bằng 0.")
            .When(x => x.OrderIndex.HasValue);
    }
}
