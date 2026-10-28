using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.States;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CulinaryBlog.UnitTests.Jobs;

public sealed class Week7FactAttribute : FactAttribute
{
    public Week7FactAttribute(bool minioDown = false)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CULINARY_WEEK7_POSTGRES")))
            Skip = "Chạy test-chung-thien-y-tuan-7.ps1 với database/container tạm.";
        else if (minioDown != (Environment.GetEnvironmentVariable("CULINARY_WEEK7_MINIO_DOWN") == "1"))
            Skip = "Ca kiểm thử thuộc pha MinIO khác của script.";
    }
}

public sealed class Week7ApiFixture : IAsyncLifetime
{
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public bool Enabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CULINARY_WEEK7_POSTGRES"));

    public async Task InitializeAsync()
    {
        if (!Enabled) return;
        var postgres = Environment.GetEnvironmentVariable("CULINARY_WEEK7_POSTGRES")!;
        if (new NpgsqlConnectionStringBuilder(postgres).Database?.StartsWith("culinary_week7_tests_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Only temporary culinary_week7_tests_* databases are permitted.");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = postgres,
            ["Redis:ConnectionString"] = Environment.GetEnvironmentVariable("CULINARY_WEEK7_REDIS"),
            ["Jwt:Secret"] = new string('x', 48), ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            ["WelcomeEmail:Host"] = Environment.GetEnvironmentVariable("CULINARY_WEEK7_SMTP_HOST") ?? "127.0.0.1",
            ["WelcomeEmail:Port"] = Environment.GetEnvironmentVariable("CULINARY_WEEK7_SMTP_PORT"),
            ["WelcomeEmail:Security"] = "None", ["WelcomeEmail:TimeoutSeconds"] = "2",
            ["MinIO:Endpoint"] = Environment.GetEnvironmentVariable("CULINARY_WEEK7_MINIO"),
            ["MinIO:BucketName"] = "recipe-images",
        });
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApiSecurity(builder.Configuration);
        builder.Services.AddCulinaryApiVersioning();
        builder.Services.AddHangfire(config => config.UsePostgreSqlStorage(
            options => options.UseNpgsqlConnection(postgres),
            new PostgreSqlStorageOptions { QueuePollInterval = TimeSpan.FromMilliseconds(200) }));
        builder.Services.AddHangfireServer(options => { options.WorkerCount = 1; options.SchedulePollingInterval = TimeSpan.FromSeconds(1); });
        App = builder.Build();
        App.UseMiddleware<GlobalExceptionMiddleware>();
        // Chỉ trong host test: mô phỏng các máy khách độc lập, vẫn giữ limiter production 10/phút.
        App.Use(async (context, next) =>
        {
            if (IPAddress.TryParse(context.Request.Headers["X-Test-Client-IP"], out var address))
                context.Connection.RemoteIpAddress = address;
            await next();
        });
        App.UseRouting(); App.UseCors(); App.UseAuthentication(); App.UseRateLimiter(); App.UseAuthorization();
        App.MapAuthEndpoints(); App.MapHealthEndpoints();
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("Author"))
                Assert.True((await roles.CreateAsync(new IdentityRole("Author"))).Succeeded);
        }
        await App.StartAsync();
        Client = new HttpClient { BaseAddress = new Uri(App.Urls.Single()) };
    }

    public async Task DisposeAsync()
    {
        if (!Enabled || App is null) return;
        Client?.Dispose();
        await App.StopAsync(); await App.DisposeAsync();
    }

    public async Task<(string UserId, string Email, string JobId)> RegisterAsync(string displayName = "Tác giả kiểm thử")
    {
        var userName = $"week7{Guid.NewGuid():N}";
        var email = $"{userName}@example.test";
        using var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        { fullName = displayName, email, userName, password = "Author@123" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var userId = json.GetProperty("data").GetProperty("user").GetProperty("id").GetString()!;
        using var connection = App.Services.GetRequiredService<JobStorage>().GetConnection();
        var monitoring = App.Services.GetRequiredService<JobStorage>().GetMonitoringApi();
        // Worker có thể đã chạy xong trước khi HTTP trả về.
        for (var attempt = 0; attempt < 100; attempt++)
        {
            // Các danh sách được đọc riêng, job có thể đổi trạng thái giữa hai lần đọc.
            var jobs = monitoring.EnqueuedJobs("default", 0, 100).Select(x => x.Key)
                .Concat(monitoring.ProcessingJobs(0, 100).Select(x => x.Key))
                .Concat(monitoring.SucceededJobs(0, 100).Select(x => x.Key))
                .Concat(monitoring.ScheduledJobs(0, 100).Select(x => x.Key))
                .Concat(monitoring.FailedJobs(0, 100).Select(x => x.Key));
            var matches = jobs.Distinct().Where(id =>
            {
                var job = connection.GetJobData(id)?.Job;
                return job?.Type == typeof(WelcomeEmailJob) && (string)job.Args[0] == userId;
            }).ToArray();
            if (matches.Length > 0) return (userId, email, Assert.Single(matches));
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Registered account did not produce a visible WelcomeEmailJob.");
    }

    public async Task WaitForStateAsync(string jobId, string expected)
    {
        var storage = App.Services.GetRequiredService<JobStorage>();
        for (var attempt = 0; attempt < 160; attempt++)
        {
            using var connection = storage.GetConnection();
            if (connection.GetStateData(jobId)?.Name == expected) return;
            await Task.Delay(250);
        }
        using var final = storage.GetConnection();
        Assert.Fail($"Expected job state {expected}; actual: {final.GetStateData(jobId)?.Name}.");
    }
}

public partial class Week7IntegrationTests(Week7ApiFixture api) : IClassFixture<Week7ApiFixture>
{
    [Week7Fact]
    public async Task Register_SendsHtmlWelcomeThroughHangfireAndMailHog()
    {
        var (_, email, jobId) = await api.RegisterAsync("<script>alert(1)</script> Tác giả");
        await api.WaitForStateAsync(jobId, SucceededState.StateName);
        using var mail = new HttpClient();
        var messages = await mail.GetFromJsonAsync<JsonElement>(Environment.GetEnvironmentVariable("CULINARY_WEEK7_MAILHOG") + "/api/v2/messages");
        var message = Assert.Single(messages.GetProperty("items").EnumerateArray().Where(x =>
            x.GetProperty("Raw").GetProperty("To").EnumerateArray().Any(to => to.GetString() == email)));
        var raw = message.GetProperty("Raw").GetProperty("Data").GetString()!;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw));
        var mime = await MimeKit.MimeMessage.LoadAsync(stream);
        Assert.Contains("Chào mừng", mime.Subject);
        Assert.Contains("Tác giả", mime.TextBody);
        Assert.Contains("http://localhost:3000", mime.HtmlBody);
        Assert.DoesNotContain("<script>", mime.HtmlBody);
        Assert.Contains("&lt;script&gt;", mime.HtmlBody);
        // Đăng ký trùng không sinh thêm job.
        using var duplicate = await api.Client.PostAsJsonAsync("/api/v1/auth/register", new
        { fullName = "Duplicate", email, userName = "duplicate", password = "Author@123" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Week7Fact]
    public async Task SmtpUnavailable_RegisterSucceeds_JobSchedulesRetry_AndRecovers()
    {
        var settings = api.App.Services.GetRequiredService<IOptions<WelcomeEmailOptions>>().Value;
        var originalPort = settings.Port;
        try
        {
            using var reservation = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            reservation.Start(); settings.Port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            var (_, email, jobId) = await api.RegisterAsync();
            await api.WaitForStateAsync(jobId, ScheduledState.StateName);
            using (var connection = api.App.Services.GetRequiredService<JobStorage>().GetConnection())
            {
                Assert.Equal("1", connection.GetJobParameter(jobId, "RetryCount"));
                var state = connection.GetStateData(jobId)!;
                var due = DateTime.Parse(state.Data["EnqueueAt"]).ToUniversalTime();
                Assert.InRange((due - DateTime.UtcNow).TotalSeconds, 10, 65);
            }
            settings.Port = originalPort;
            // Đưa job scheduled vào queue ngay để test không chờ 1 phút; policy production giữ nguyên.
            Assert.True(api.App.Services.GetRequiredService<IBackgroundJobClient>().ChangeState(jobId, new EnqueuedState(), ScheduledState.StateName));
            await api.WaitForStateAsync(jobId, SucceededState.StateName);
        }
        finally { settings.Port = originalPort; }
    }

    [Week7Fact]
    public async Task Readiness_AllDependenciesAvailable_Returns200()
    {
        using var response = await api.Client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await api.App.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        Assert.Equal(HealthStatus.Healthy, report.Entries["minio"].Status);
        // Bucket khác không tồn tại phải bị phát hiện, không chỉ ping process.
        var minio = api.App.Services.GetRequiredService<IOptions<MinioOptions>>().Value;
        var result = await new MinioHealthCheck(Options.Create(new MinioOptions
        { Endpoint = minio.Endpoint, BucketName = "nonexistent-week7-bucket" })).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Week7Fact(true)]
    public async Task Readiness_MinIoStopped_Returns503_WhileLivenessRemains200()
    {
        using var ready = await api.Client.GetAsync("/health/ready");
        using var overall = await api.Client.GetAsync("/health");
        using var live = await api.Client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, overall.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        var report = await api.App.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        Assert.Equal(HealthStatus.Unhealthy, report.Entries["minio"].Status);
        Assert.Equal(HealthStatus.Healthy, report.Entries["postgresql"].Status);
        Assert.Equal(HealthStatus.Healthy, report.Entries["redis"].Status);
    }
}
