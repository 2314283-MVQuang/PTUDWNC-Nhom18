using FluentValidation;

namespace CulinaryBlog.Application.Features.Auth.Commands.Register;

/// <summary>
/// Rule theo đặc tả FR-AUTH-001 / NFR-SEC-001 (Buổi 2):
/// - email đúng định dạng
/// - mật khẩu ≥ 8 ký tự, có hoa, thường, số, ký tự đặc biệt
/// </summary>
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.UserName)
            .Matches("^[a-zA-Z0-9_.]+$").When(x => !string.IsNullOrEmpty(x.UserName))
            .WithMessage("Tên đăng nhập chỉ được chứa chữ, số, dấu gạch dưới và dấu chấm.")
            .MinimumLength(3).When(x => !string.IsNullOrEmpty(x.UserName))
            .MaximumLength(50).When(x => !string.IsNullOrEmpty(x.UserName));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số.")
            .Matches(@"[!@#$%^&*(),.?"":{}|<>_\-+=\[\]/\\;'~`]").WithMessage("Mật khẩu phải có ít nhất 1 ký tự đặc biệt.");
    }
}
