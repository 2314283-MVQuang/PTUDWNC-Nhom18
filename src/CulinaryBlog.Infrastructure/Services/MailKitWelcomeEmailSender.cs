using System.Text.Encodings.Web;
using CulinaryBlog.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CulinaryBlog.Infrastructure.Services;

public sealed class MailKitWelcomeEmailSender(IOptions<WelcomeEmailOptions> options) : IWelcomeEmailSender
{
    public async Task SendAsync(string toEmail, string displayName, CancellationToken ct = default)
    {
        var settings = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
        message.To.Add(new MailboxAddress(displayName, toEmail));
        message.Subject = "Chào mừng đến với Culinary Blog";
        var name = string.IsNullOrWhiteSpace(displayName) ? "bạn" : displayName;
        message.Body = new BodyBuilder
        {
            TextBody = $"Xin chào {name},\n\nChào mừng đến với Culinary Blog! Tài khoản đã được tạo thành công. Hãy khám phá và chia sẻ những công thức yêu thích.\nTruy cập ứng dụng: {settings.AppUrl}\n\nCulinary Blog",
            HtmlBody = $"<h1>Chào mừng đến với Culinary Blog</h1><p>Xin chào {HtmlEncoder.Default.Encode(name)},</p><p>Tài khoản đã được tạo thành công. Hãy khám phá và chia sẻ những công thức yêu thích.</p><p><a href=\"{HtmlEncoder.Default.Encode(settings.AppUrl)}\">Khám phá Culinary Blog</a></p><p>Culinary Blog</p>",
        }.ToMessageBody();

        using var client = new SmtpClient { Timeout = settings.TimeoutSeconds * 1000 };
        await client.ConnectAsync(settings.Host, settings.Port, settings.Security, ct);
        if (!string.IsNullOrWhiteSpace(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password!, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
        // Không nuốt lỗi SMTP: Hangfire cần nhận exception để thực hiện retry.
    }
}
