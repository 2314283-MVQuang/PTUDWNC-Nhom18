namespace CulinaryBlog.Infrastructure.Services;

/// <summary>Đọc từ section "Google" trong appsettings — ClientId dùng để kiểm tra idToken
/// đúng là phát hành cho app này (audience), chống việc dùng idToken của app khác.</summary>
public class GoogleOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = null!;
}