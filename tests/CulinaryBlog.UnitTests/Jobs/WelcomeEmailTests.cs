using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Auth.Commands.Register;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using CulinaryBlog.Infrastructure.Services;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.UnitTests.Jobs;

public class WelcomeEmailTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Register_QueuesAfterCommit_AndStillSucceedsWhenQueueFails(bool queueFails)
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Author"))).Succeeded);
        var scheduler = new RecordingScheduler(db) { Throw = queueFails };
        var confirmation = new RecordingAuthEmail();
        var handler = Handler(scope.ServiceProvider, scheduler, confirmation);
        var result = await handler.Handle(new RegisterCommand("Tác giả", "author@example.test", "author", "Author@123"), default);
        Assert.Equal(1, scheduler.Calls);
        Assert.Equal(result.User.Id, scheduler.UserId);
        Assert.True(scheduler.WasCommitted);
        Assert.Equal(1, confirmation.Confirmations);
        Assert.Equal(0, confirmation.Welcomes);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));
    }

    [Fact]
    public async Task Register_DuplicateOrInvalidPassword_DoesNotQueue()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("Author"));
        var scheduler = new RecordingScheduler(db);
        var handler = Handler(scope.ServiceProvider, scheduler, new RecordingAuthEmail());
        var command = new RegisterCommand("Author", "author@example.test", "author", "Author@123");
        await handler.Handle(command, default);
        await Assert.ThrowsAsync<CulinaryBlog.Application.Common.Exceptions.ConflictException>(() => handler.Handle(command, default));
        await Assert.ThrowsAsync<CulinaryBlog.Application.Common.Exceptions.ValidationException>(() => handler.Handle(command with { Email = "other@example.test", UserName = "other", Password = "x" }, default));
        Assert.Equal(1, scheduler.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Job_OnlySendsForExistingActiveUser(bool exists, bool active)
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        if (exists) { db.Users.Add(new ApplicationUser { Id = "user", Email = "author@example.test", DisplayName = "Tác giả", IsActive = active }); await db.SaveChangesAsync(); }
        var sender = new RecordingWelcomeSender();
        await new WelcomeEmailJob(db, sender, NullLogger<WelcomeEmailJob>.Instance).ExecuteAsync("user");
        Assert.Equal(exists && active ? 1 : 0, sender.Calls);
    }

    [Fact]
    public async Task Job_PropagatesSmtpErrors_ForHangfireRetry()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        db.Users.Add(new ApplicationUser { Id = "user", Email = "author@example.test", DisplayName = "Author" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<IOException>(() => new WelcomeEmailJob(db, new RecordingWelcomeSender { Throw = true }, NullLogger<WelcomeEmailJob>.Instance).ExecuteAsync("user"));
    }

    [Fact]
    public void RetryPolicy_MatchesSrs_AndRetainsFailedJobs()
    {
        var attribute = Assert.Single(typeof(WelcomeEmailJob).GetMethod(nameof(WelcomeEmailJob.ExecuteAsync))!
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), false).Cast<AutomaticRetryAttribute>());
        Assert.Equal(3, attribute.Attempts);
        Assert.Equal(new[] { 60, 300, 1800 }, attribute.DelaysInSeconds);
        Assert.Equal(AttemptsExceededAction.Fail, attribute.OnAttemptsExceeded);
    }

    [Theory]
    [InlineData("", 1025, "http://localhost:3000")]
    [InlineData("localhost", 0, "http://localhost:3000")]
    [InlineData("localhost", 65536, "http://localhost:3000")]
    [InlineData("localhost", 1025, "javascript:alert(1)")]
    public void InvalidSmtpConfiguration_IsRejected(string host, int port, string url) =>
        Assert.False(new WelcomeEmailOptions { Host = host, Port = port, AppUrl = url }.IsValid());

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDataProtection();
        services.AddDbContext<CulinaryBlogDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<CulinaryBlogDbContext>().AddDefaultTokenProviders();
        return services.BuildServiceProvider();
    }

    private static RegisterCommandHandler Handler(IServiceProvider services, IWelcomeEmailScheduler scheduler, IEmailService email)
    {
        var db = services.GetRequiredService<CulinaryBlogDbContext>();
        return new RegisterCommandHandler(services.GetRequiredService<UserManager<ApplicationUser>>(),
            new JwtService(Options.Create(new JwtOptions { Secret = new string('x', 48) })),
            new RefreshTokenRepository(db), db, email, scheduler, NullLogger<RegisterCommandHandler>.Instance);
    }

    private sealed class RecordingScheduler(CulinaryBlogDbContext db) : IWelcomeEmailScheduler
    {
        public int Calls; public string? UserId; public bool WasCommitted; public bool Throw;
        public string Enqueue(string userId)
        {
            Calls++; UserId = userId;
            WasCommitted = db.Users.AsNoTracking().Any(x => x.Id == userId) && db.RefreshTokens.AsNoTracking().Any(x => x.UserId == userId);
            if (Throw) throw new IOException("Queue unavailable");
            return "job";
        }
    }
    private sealed class RecordingAuthEmail : IEmailService
    {
        public int Welcomes; public int Confirmations;
        public Task SendWelcomeEmailAsync(string email, string name, CancellationToken ct = default) { Welcomes++; return Task.CompletedTask; }
        public Task SendEmailConfirmationAsync(string email, string name, string token, CancellationToken ct = default) { Confirmations++; return Task.CompletedTask; }
        public Task SendPasswordResetEmailAsync(string email, string name, string token, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class RecordingWelcomeSender : IWelcomeEmailSender
    {
        public int Calls; public bool Throw;
        public Task SendAsync(string email, string name, CancellationToken ct = default)
        { Calls++; return Throw ? Task.FromException(new IOException("SMTP unavailable")) : Task.CompletedTask; }
    }
}
