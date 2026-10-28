using MailKit.Security;
using MimeKit;

namespace CulinaryBlog.Infrastructure.Services;

public sealed class WelcomeEmailOptions
{
    public const string SectionName = "WelcomeEmail";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string FromEmail { get; set; } = "noreply@culinaryblog.local";
    public string FromName { get; set; } = "Culinary Blog";
    public string AppUrl { get; set; } = "http://localhost:3000";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
    public int TimeoutSeconds { get; set; } = 10;

    public bool IsValid() => !string.IsNullOrWhiteSpace(Host)
        && Port is > 0 and <= 65535 && TimeoutSeconds is >= 1 and <= 120
        && MailboxAddress.TryParse(FromEmail, out _)
        && Enum.IsDefined(Security)
        && Uri.TryCreate(AppUrl, UriKind.Absolute, out var appUri)
        && (appUri.Scheme is "http" or "https")
        && string.IsNullOrWhiteSpace(Username) == string.IsNullOrWhiteSpace(Password);
}
