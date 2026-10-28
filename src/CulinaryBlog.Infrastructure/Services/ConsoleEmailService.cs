using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// Adapter giả lập cũ, hiện chỉ được MailKitAuthEmailService dùng cho xác nhận địa chỉ email.
/// Thư đặt lại mật khẩu thực tế đi qua MailKitAuthEmailService; WelcomeEmailJob dùng
/// MailKitWelcomeEmailSender. Các phương thức cũ giữ lại để tương thích interface.
///
/// Tuần 3: thêm log cho 2 method mới (reset mật khẩu, xác nhận email) — CÙNG cách làm TẠM, chỉ in
/// token ra log thay vì dựng link đầy đủ trỏ về frontend (frontend build link + gọi API tương ứng
/// khi bấm nút trong email — base URL frontend chưa cấu hình ở backend nên chưa ghép được link).
/// </summary>
public class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendWelcomeEmailAsync(string toEmail, string displayName, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EMAIL GIẢ LẬP] Gửi email chào mừng tới {Email} (tên hiển thị: {DisplayName})",
            toEmail, displayName);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string displayName, string resetToken, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EMAIL GIẢ LẬP] Gửi email đặt lại mật khẩu tới {Email} (tên hiển thị: {DisplayName}) — " +
            "token (dùng cho POST /api/v1/auth/reset-password): {ResetToken}",
            toEmail, displayName, resetToken);

        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(string toEmail, string displayName, string confirmationToken, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EMAIL GIẢ LẬP] Gửi email xác nhận địa chỉ tới {Email} (tên hiển thị: {DisplayName}) — " +
            "token (dùng cho POST /api/v1/auth/confirm-email): {ConfirmationToken}",
            toEmail, displayName, confirmationToken);

        return Task.CompletedTask;
    }
}
