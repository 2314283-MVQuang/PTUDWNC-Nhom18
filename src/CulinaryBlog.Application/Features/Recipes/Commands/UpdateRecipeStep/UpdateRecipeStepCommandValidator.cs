using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeStep;

public class UpdateRecipeStepCommandValidator : AbstractValidator<UpdateRecipeStepCommand>
{
    public UpdateRecipeStepCommandValidator()
    {
        RuleFor(x => x.RecipeId)
            .NotEmpty().WithMessage("RecipeId không được để trống.");

        RuleFor(x => x.StepId)
            .NotEmpty().WithMessage("StepId không được để trống.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề bước thực hiện không được để trống.")
            .MaximumLength(200).WithMessage("Tiêu đề bước tối đa 200 ký tự.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Mô tả bước thực hiện không được để trống.")
            .MaximumLength(2000).WithMessage("Mô tả bước tối đa 2000 ký tự.");

        RuleFor(x => x.StepNumber)
            .GreaterThan(0).WithMessage("Số thứ tự bước phải lớn hơn 0.")
            .When(x => x.StepNumber.HasValue);

        RuleFor(x => x.TimerMinutes)
            .GreaterThan(0).WithMessage("Thời gian hẹn giờ (phút) phải lớn hơn 0.")
            .When(x => x.TimerMinutes.HasValue);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500).WithMessage("Đường dẫn ảnh tối đa 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));
    }
}
