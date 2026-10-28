using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.UnitTests.Jobs;

public partial class Week7IntegrationTests
{
    [Week7Fact]
    public async Task Account_RegisterLoginLogout_RejectsInvalidCredentialsAndRevokedRefresh()
    {
        using var client = AccountClient(11);
        var account = await RegisterAccount(client);
        using var anonymous = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new { account.Email, password = "Wrong@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { account.Email, password = "Author@123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        var refreshToken = tokens.GetProperty("refreshToken").GetString();
        for (var i = 0; i < 2; i++)
        {
            using var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken });
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Week7Fact]
    public async Task Account_Profile_PersistsUpdatesAndClearsOptionalFields()
    {
        using var client = AccountClient(12);
        var account = await RegisterAccount(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        using var saved = await client.PatchAsJsonAsync("/api/v1/auth/me", new
        { displayName = "Tên đã cập nhật", bio = "Giới thiệu mới", avatarUrl = "https://example.test/avatar.png" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var profile = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Tên đã cập nhật", profile.GetProperty("displayName").GetString());
        using var cleared = await client.PatchAsJsonAsync("/api/v1/auth/me", new { bio = "", avatarUrl = "" });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var current = (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("data");
        Assert.Equal(account.Email, current.GetProperty("email").GetString());
        Assert.Equal("Tên đã cập nhật", current.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("bio").ValueKind);
        Assert.Equal(JsonValueKind.Null, current.GetProperty("avatarUrl").ValueKind);
        using var invalid = await client.PatchAsJsonAsync("/api/v1/auth/me", new { displayName = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    [Week7Fact]
    public async Task Account_ChangePassword_RequiresCurrentPassword_AndChangesLoginCredentials()
    {
        using var client = AccountClient(13);
        var account = await RegisterAccount(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        using var wrong = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Wrong@123", newPassword = "Changed@456" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        using var changed = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Author@123", newPassword = "Changed@456" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        using var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { account.Email, password = "Author@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { account.Email, password = "Changed@456" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Week7Fact]
    public async Task Account_ForgotReset_UsesSmtpLink_RejectsInvalidAndReusedTokens()
    {
        using var client = AccountClient(14);
        var account = await RegisterAccount(client);
        using var unknown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = $"missing{Guid.NewGuid():N}@example.test" });
        using var requested = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { account.Email });
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, requested.StatusCode);
        using var mailbox = new HttpClient();
        var messages = await mailbox.GetFromJsonAsync<JsonElement>(Environment.GetEnvironmentVariable("CULINARY_WEEK7_MAILHOG") + "/api/v2/messages");
        var rawMessages = messages.GetProperty("items").EnumerateArray().Where(x =>
            x.GetProperty("Raw").GetProperty("To").EnumerateArray().Any(to => to.GetString() == account.Email));
        MimeKit.MimeMessage? resetMail = null;
        foreach (var rawMessage in rawMessages)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rawMessage.GetProperty("Raw").GetProperty("Data").GetString()!));
            var mime = await MimeKit.MimeMessage.LoadAsync(stream);
            if (mime.Subject?.StartsWith("Đặt lại") == true) resetMail = mime;
        }
        Assert.NotNull(resetMail);
        Assert.DoesNotContain("<script>", resetMail.HtmlBody);
        Assert.Contains("&lt;script&gt;", resetMail.HtmlBody);
        var link = (resetMail.TextBody ?? throw new InvalidOperationException("Missing text email body."))
            .Split('\n', StringSplitOptions.TrimEntries).Single(x => x.StartsWith("http"));
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(link).Query);
        Assert.Equal(account.Email, query["email"].ToString()); // Email có dấu + vẫn nguyên vẹn.
        var resetToken = query["token"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(resetToken));
        using var invalid = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { account.Email, token = "invalid", newPassword = "Reset@456" });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        var body = new { account.Email, token = resetToken, newPassword = "Reset@456" };
        using var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", body);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var reused = await client.PostAsJsonAsync("/api/v1/auth/reset-password", body);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        using var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { account.Email, password = "Author@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { account.Email, password = "Reset@456" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Week7Fact]
    public async Task Account_ForgotPassword_SmtpFailureKeepsGenericResponse()
    {
        using var client = AccountClient(15);
        var account = await RegisterAccount(client);
        var settings = api.App.Services.GetRequiredService<IOptions<WelcomeEmailOptions>>().Value;
        var originalPort = settings.Port;
        try
        {
            using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            socket.Start(); settings.Port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
            using var failedSmtp = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { account.Email });
            using var unknown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "unknown@example.test" });
            Assert.Equal(HttpStatusCode.NoContent, failedSmtp.StatusCode);
            Assert.Equal(failedSmtp.StatusCode, unknown.StatusCode);
        }
        finally { settings.Port = originalPort; }
    }

    private HttpClient AccountClient(int address)
    {
        var client = new HttpClient { BaseAddress = api.Client.BaseAddress };
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", $"192.0.2.{address}");
        return client;
    }

    private static async Task<(string Email, string AccessToken)> RegisterAccount(HttpClient client)
    {
        var userName = $"account{Guid.NewGuid():N}";
        var email = $"{userName}+test@example.test";
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        { fullName = "<script>alert(1)</script> Tác giả", email, userName, password = "Author@123" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        return (email, data.GetProperty("accessToken").GetString()!);
    }
}
