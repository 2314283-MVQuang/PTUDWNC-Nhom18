using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;

/// <summary>
/// Validator của CreateRecipeCommand.
/// ValidationBehavior của MediatR sẽ tự động chạy class này.
/// </summary>
public sealed class CreateRecipeCommandValidator
    : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Tên công thức không được để trống.")
            .MaximumLength(200)
            .WithMessage("Tên công thức tối đa 200 ký tự.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Mô tả không được để trống.")
            .MaximumLength(2000)
            .WithMessage("Mô tả tối đa 2000 ký tự.");

        RuleFor(x => x.CategoryId)
            .NotEmpty()
            .WithMessage("CategoryId không được để trống.");

        RuleFor(x => x.PrepTime)
            .GreaterThan(0)
            .WithMessage("Thời gian chuẩn bị phải lớn hơn 0 phút.");

        RuleFor(x => x.CookTime)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Thời gian nấu không được âm.");

        RuleFor(x => x.Servings)
            .GreaterThan(0)
            .WithMessage("Số khẩu phần phải lớn hơn 0.");

        RuleFor(x => x.Difficulty)
            .IsInEnum()
            .WithMessage("Độ khó không hợp lệ.");

        RuleFor(x => x.Instructions)
            .NotEmpty()
            .WithMessage("Instructions không được để trống.")
            .MaximumLength(10000)
            .WithMessage("Instructions tối đa 10000 ký tự.");

        RuleForEach(x => x.Steps)
            .SetValidator(new CreateRecipeStepInputValidator());

        RuleFor(x => x.Steps)
            .Must(HaveUniqueStepNumbers)
            .When(x => x.Steps is not null)
            .WithMessage("StepNumber của các bước không được trùng nhau.");

        RuleForEach(x => x.Ingredients)
            .SetValidator(new CreateRecipeIngredientInputValidator());

        RuleFor(x => x.Ingredients)
            .Must(HaveUniqueOrderIndexes)
            .When(x => x.Ingredients is not null)
            .WithMessage("OrderIndex của các nguyên liệu không được trùng nhau.");
    }

    private static bool HaveUniqueStepNumbers(
        CreateRecipeStepInput[]? steps)
    {
        if (steps is null)
        {
            return true;
        }

        return steps
            .Select(x => x.StepNumber)
            .Distinct()
            .Count() == steps.Length;
    }

    private static bool HaveUniqueOrderIndexes(
        CreateRecipeIngredientInput[]? ingredients)
    {
        if (ingredients is null)
        {
            return true;
        }

        return ingredients
            .Select(x => x.OrderIndex)
            .Distinct()
            .Count() == ingredients.Length;
    }
}

public sealed class CreateRecipeStepInputValidator
    : AbstractValidator<CreateRecipeStepInput>
{
    public CreateRecipeStepInputValidator()
    {
        RuleFor(x => x.StepNumber)
            .GreaterThan(0)
            .WithMessage("StepNumber phải lớn hơn 0.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Tiêu đề bước không được để trống.")
            .MaximumLength(200)
            .WithMessage("Tiêu đề bước tối đa 200 ký tự.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Mô tả bước không được để trống.")
            .MaximumLength(2000)
            .WithMessage("Mô tả bước tối đa 2000 ký tự.");

        RuleFor(x => x.TimerMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TimerMinutes.HasValue)
            .WithMessage("TimerMinutes không được âm.");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .When(x => x.ImageUrl is not null)
            .WithMessage("ImageUrl tối đa 500 ký tự.");
    }
}

public sealed class CreateRecipeIngredientInputValidator
    : AbstractValidator<CreateRecipeIngredientInput>
{
    public CreateRecipeIngredientInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Tên nguyên liệu không được để trống.")
            .MaximumLength(200)
            .WithMessage("Tên nguyên liệu tối đa 200 ký tự.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .When(x => x.Quantity.HasValue)
            .WithMessage("Quantity phải lớn hơn 0 nếu có nhập.");

        RuleFor(x => x.Unit)
            .MaximumLength(50)
            .When(x => x.Unit is not null)
            .WithMessage("Unit tối đa 50 ký tự.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes is not null)
            .WithMessage("Notes tối đa 500 ký tự.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0)
            .WithMessage("OrderIndex phải >= 0.");
    }
}