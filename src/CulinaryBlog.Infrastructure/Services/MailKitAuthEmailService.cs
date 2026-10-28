using System.Text.Encodings.Web;
using CulinaryBlog.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>Email đặt lại mật khẩu dùng SMTP hiện có; luồng xác nhận email giữ nguyên phạm vi.</summary>
public sealed class MailKitAuthEmailService(
    IOptions<WelcomeEmailOptions> options,
    IWelcomeEmailSender welcome,
    ConsoleEmailService confirmation) : IEmailService
{
    public Task SendWelcomeEmailAsync(string toEmail, string displayName, CancellationToken ct = default) =>
        welcome.SendAsync(toEmail, displayName, ct);

    public Task SendEmailConfirmationAsync(string toEmail, string displayName, string confirmationToken, CancellationToken ct = default) =>
        confirmation.SendEmailConfirmationAsync(toEmail, displayName, confirmationToken, ct);

    public async Task SendPasswordResetEmailAsync(string toEmail, string displayName, string resetToken, CancellationToken ct = default)
    {
        var settings = options.Value;
        // URL-encode từng giá trị để giữ nguyên token có +, /, = và email có dấu +.
        var link = settings.AppUrl.TrimEnd('/') + "/reset-password?email=" + Uri.EscapeDataString(toEmail)
            + "&token=" + Uri.EscapeDataString(resetToken);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
        message.To.Add(new MailboxAddress(displayName, toEmail));
        message.Subject = "Đặt lại mật khẩu Culinary Blog";
        message.Body = new BodyBuilder
        {
            TextBody = $"Xin chào {displayName},\n\nMở liên kết để đặt lại mật khẩu:\n{link}\n\nNếu không gửi yêu cầu này, có thể bỏ qua thư.",
            HtmlBody = $"<p>Xin chào {HtmlEncoder.Default.Encode(displayName)},</p><p><a href=\"{HtmlEncoder.Default.Encode(link)}\">Đặt lại mật khẩu</a></p><p>Nếu không gửi yêu cầu này, có thể bỏ qua thư.</p>",
        }.ToMessageBody();
        using var client = new SmtpClient { Timeout = settings.TimeoutSeconds * 1000 };
        await client.ConnectAsync(settings.Host, settings.Port, settings.Security, ct);
        if (!string.IsNullOrWhiteSpace(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password!, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
        // Không ghi link hoặc token đặt lại mật khẩu vào log.
    }
}
