namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// FR-JOB-001: Welcome Email Job. TODO (nhóm làm tiếp): bản hiện tại gọi service này TRỰC TIẾP
/// (đồng bộ) trong RegisterCommandHandler để scaffold chạy được không cần Hangfire. Khi tích hợp
/// Hangfire, đổi lời gọi ở RegisterCommandHandler thành
/// <c>BackgroundJob.Enqueue(() => emailService.SendWelcomeEmailAsync(...))</c> (fire-and-forget).
///
/// Tuần 3: thêm 2 method cho ForgotPassword/ResetPassword (FR-AUTH nâng cao) và xác nhận email
/// lúc đăng ký — cùng interface, cùng implementation TẠM (ConsoleEmailService) như welcome email,
/// chỉ khác nội dung/token gửi kèm.
/// </summary>
public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string displayName, CancellationToken ct = default);

    /// <summary>Gửi link/token đặt lại mật khẩu (ForgotPasswordCommandHandler). Token do
    /// UserManager.GeneratePasswordResetTokenAsync sinh, tự hết hạn theo cấu hình Identity
    /// (mặc định 1 ngày) — implementation chỉ cần log/gửi nguyên văn, KHÔNG tự thêm logic hết hạn.</summary>
    Task SendPasswordResetEmailAsync(string toEmail, string displayName, string resetToken, CancellationToken ct = default);

    /// <summary>Gửi link/token xác nhận email sau khi đăng ký (RegisterCommandHandler gọi ngay
    /// sau khi tạo user). Token do UserManager.GenerateEmailConfirmationTokenAsync sinh.</summary>
    Task SendEmailConfirmationAsync(string toEmail, string displayName, string confirmationToken, CancellationToken ct = default);
}
