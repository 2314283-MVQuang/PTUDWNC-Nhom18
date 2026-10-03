using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeIngredient;

public class UpdateRecipeIngredientCommandValidator : AbstractValidator<UpdateRecipeIngredientCommand>
{
    public UpdateRecipeIngredientCommandValidator()
    {
        RuleFor(x => x.RecipeId)
            .NotEmpty().WithMessage("RecipeId không được để trống.");

        RuleFor(x => x.IngredientId)
            .NotEmpty().WithMessage("IngredientId không được để trống.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên nguyên liệu không được để trống.")
            .MaximumLength(200).WithMessage("Tên nguyên liệu tối đa 200 ký tự.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.")
            .When(x => x.Quantity.HasValue);

        RuleFor(x => x.Unit)
            .MaximumLength(50).WithMessage("Đơn vị tối đa 50 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Unit));

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Thứ tự sắp xếp phải lớn hơn hoặc bằng 0.")
            .When(x => x.OrderIndex.HasValue);
    }
}
