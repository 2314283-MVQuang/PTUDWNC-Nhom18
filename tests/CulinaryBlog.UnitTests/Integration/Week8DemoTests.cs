using System.Net;
using System.Text.Json;
using Xunit;
using static CulinaryBlog.UnitTests.Integration.Week8ApiFixture;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.UnitTests.Integration;

[Collection("Week8Integration")]
public sealed class Week8DemoTests(Week8ApiFixture api) : IClassFixture<Week8ApiFixture>
{
    [Week8DemoFact]
    public async Task HoldIsolatedApiForManualAuthRecipeDemo()
    {
        var directory = Environment.GetEnvironmentVariable("CULINARY_WEEK8_DEMO_DIRECTORY")
            ?? throw new InvalidOperationException("Demo directory is required.");
        using var client = api.NewClient();
        var owner = await api.RegisterAsync(client);
        var other = await api.RegisterAsync(client);
        var admin = await api.RegisterAsync(client);
        await api.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await manager.AddToRoleAsync((await manager.FindByIdAsync(admin.UserId))!, "Admin")).Succeeded);
            for(var i=1;i<=14;i++)
            {
                var user = new ApplicationUser { UserName=$"demo{i}{Guid.NewGuid():N}", Email=$"demo{i}{Guid.NewGuid():N}@example.test", DisplayName=$"Tác giả minh họa {i:00}" };
                Assert.True((await manager.CreateAsync(user)).Succeeded);
                Assert.True((await manager.AddToRoleAsync(user,"Author")).Succeeded);
            }
        });
        using var create = await SendAsync(client, HttpMethod.Post, "/api/v1/recipes",
            api.RecipeBody(title: "Phở bò mẫu tuần 8"), owner.AccessToken);
        var draft = await DataAsync(create, HttpStatusCode.Created);
        var manifest = new
        {
            apiBaseUrl = api.App.Urls.Single() + "/api/v1",
            categoryId = api.CategoryId,
            categoryName = "Món nước tuần 8",
            ownerEmail = owner.Email, otherEmail = other.Email, password = Password,
            adminEmail = admin.Email,
            minioEndpoint = Environment.GetEnvironmentVariable("CULINARY_WEEK8_MINIO"),
            logDirectory = api.LogDirectory,
            draftId = draft.GetProperty("id").GetGuid(), draftSlug = draft.GetProperty("slug").GetString(),
            note = "Tài khoản dùng riêng trong container tạm; tự mất sau khi dừng demo. Không chứa JWT/refresh token.",
        };
        var path = Path.Combine(directory, "demo.json");
        await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path);
        var stop = Path.Combine(directory, "stop");
        // Tránh để test host chờ vô hạn khi cửa sổ điều khiển bị đóng đột ngột.
        var deadline = DateTimeOffset.UtcNow.AddMinutes(30);
        while (!File.Exists(stop) && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(250);
    }
}
