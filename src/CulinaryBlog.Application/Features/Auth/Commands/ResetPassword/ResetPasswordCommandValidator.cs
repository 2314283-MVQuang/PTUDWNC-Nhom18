using FluentValidation;

namespace CulinaryBlog.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Thiếu token đặt lại mật khẩu.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu mới phải có ít nhất 1 chữ hoa.")
            .Matches("[0-9]").WithMessage("Mật khẩu mới phải có ít nhất 1 chữ số.")
            .Matches(@"[!@#$%^&*(),.?"":{}|<>_\-+=\[\]/\\;'~`]").WithMessage("Mật khẩu mới phải có ít nhất 1 ký tự đặc biệt.");
    }
}
