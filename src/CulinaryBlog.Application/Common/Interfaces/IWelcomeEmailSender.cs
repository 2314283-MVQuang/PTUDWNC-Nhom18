namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>Gửi thư chào mừng; độc lập với luồng xác nhận email/đặt lại mật khẩu.</summary>
public interface IWelcomeEmailSender
{
    Task SendAsync(string toEmail, string displayName, CancellationToken ct = default);
}
