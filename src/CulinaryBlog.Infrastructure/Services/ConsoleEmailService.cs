using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// TODO (nhóm làm tiếp): implementation TẠM THỜI của IEmailService — chỉ ghi log thay vì gửi mail
/// thật, để scaffold chạy được ngay không cần cấu hình SMTP. Khi triển khai FR-JOB-001 thật, viết
/// MailKitEmailService (SMTP dev qua MailHog, SendGrid production — mục 3.1) implement interface
/// NÀY, rồi đổi đăng ký DI ở Infrastructure/DependencyInjection.cs.
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
