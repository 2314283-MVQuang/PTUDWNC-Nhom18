namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// Email Auth hiện có. FR-JOB-001 sử dụng IWelcomeEmailScheduler và IWelcomeEmailSender riêng,
/// đặt lại mật khẩu gửi SMTP qua MailKitAuthEmailService; xác nhận email giữ cách hiện có.
///
/// Tuần 3: thêm 2 method cho ForgotPassword/ResetPassword (FR-AUTH nâng cao) và xác nhận email
/// lúc đăng ký. Phương thức welcome cũ được giữ để
/// tương thích; luồng đăng ký mới lên lịch qua IWelcomeEmailScheduler.
/// </summary>
public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string displayName, CancellationToken ct = default);

    /// <summary>Gửi link/token đặt lại mật khẩu (ForgotPasswordCommandHandler). Token do
    /// UserManager.GeneratePasswordResetTokenAsync sinh, tự hết hạn theo cấu hình Identity
    /// (mặc định 1 ngày); implementation gửi link, không tự thêm logic hết hạn.</summary>
    Task SendPasswordResetEmailAsync(string toEmail, string displayName, string resetToken, CancellationToken ct = default);

    /// <summary>Gửi link/token xác nhận email sau khi đăng ký (RegisterCommandHandler gọi ngay
    /// sau khi tạo user). Token do UserManager.GenerateEmailConfirmationTokenAsync sinh.</summary>
    Task SendEmailConfirmationAsync(string toEmail, string displayName, string confirmationToken, CancellationToken ct = default);
}
