using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CulinaryBlog.UnitTests.Integration.Week8ApiFixture;

namespace CulinaryBlog.UnitTests.Integration;

[Collection("Week8Integration")]
public sealed class Lab8ApiTests(Week8ApiFixture api) : IClassFixture<Week8ApiFixture>
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a6V0AAAAASUVORK5CYII=");

    [Week8Fact]
    public async Task Images_UploadPublicReadPrimaryDelete_UsesRealMinioAndCleanupJob()
    {
        using var client = api.NewClient(); var account = await api.RegisterAsync(client);
        using var create = await SendAsync(client,HttpMethod.Post,"/api/v1/recipes",api.RecipeBody(),account.AccessToken);
        var draft = await DataAsync(create,HttpStatusCode.Created); var id = draft.GetProperty("id").GetGuid();
        using var firstUpload = await Upload(client,id,account.AccessToken,Png,"dish.png","image/png");
        var first = await DataAsync(firstUpload,HttpStatusCode.Created);
        Assert.True(first.GetProperty("isPrimary").GetBoolean());
        using var objectResponse = await client.GetAsync(first.GetProperty("originalUrl").GetString());
        Assert.Equal(HttpStatusCode.OK,objectResponse.StatusCode);
        Assert.Equal(Png,await objectResponse.Content.ReadAsByteArrayAsync());
        // URL ngoài dù trùng đường dẫn cũng không được làm mất object của công thức.
        var externalUrl = new UriBuilder(first.GetProperty("originalUrl").GetString()!) { Host = "example.invalid" }.Uri.ToString();
        await api.InScopeAsync(services => services.GetRequiredService<IRecipeImageStorage>().DeleteAsync(id,externalUrl,CancellationToken.None));
        using var preserved = await client.GetAsync(first.GetProperty("originalUrl").GetString());
        Assert.Equal(HttpStatusCode.OK,preserved.StatusCode);
        using var secondUpload = await Upload(client,id,account.AccessToken,Png,"second.png","image/png");
        var second = await DataAsync(secondUpload,HttpStatusCode.Created);
        Assert.False(second.GetProperty("isPrimary").GetBoolean());
        using var primary = await SendAsync(client,HttpMethod.Patch,$"/api/v1/recipes/{id}/images/{second.GetProperty("id").GetGuid()}/primary",token:account.AccessToken);
        Assert.True((await DataAsync(primary)).GetProperty("isPrimary").GetBoolean());
        using var delete = await SendAsync(client,HttpMethod.Delete,$"/api/v1/recipes/{id}/images/{second.GetProperty("id").GetGuid()}",token:account.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent,delete.StatusCode);
        using var list = await SendAsync(client,HttpMethod.Get,$"/api/v1/recipes/{id}/images",token:account.AccessToken);
        var only = Assert.Single((await DataAsync(list)).EnumerateArray());
        Assert.Equal(first.GetProperty("id").GetGuid(),only.GetProperty("id").GetGuid());
        Assert.True(only.GetProperty("isPrimary").GetBoolean());
        var removed = false;
        for (var attempt=0;attempt<80;attempt++)
        {
            using var check = await client.GetAsync(second.GetProperty("originalUrl").GetString());
            if (check.StatusCode == HttpStatusCode.NotFound) { removed=true; break; }
            await Task.Delay(250);
        }
        Assert.True(removed,"Hangfire should remove the deleted image object from temporary MinIO.");

        // Nhánh nhóm có luồng presigned + JSON metadata; vẫn gắn được ảnh đã tải lên.
        using var legacyCreate = await SendAsync(client,HttpMethod.Post,"/api/v1/recipes",api.RecipeBody(),account.AccessToken);
        var legacyRecipeId = (await DataAsync(legacyCreate,HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using var attachment = await SendAsync(client,HttpMethod.Post,$"/api/v1/recipes/{legacyRecipeId}/images",
            new { imageUrl = first.GetProperty("originalUrl").GetString(), altText = "Ảnh qua luồng tương thích" },account.AccessToken);
        Assert.True((await DataAsync(attachment,HttpStatusCode.Created)).GetProperty("isPrimary").GetBoolean());
    }

    [Week8Theory]
    [InlineData("fake.png","image/png",false)]
    [InlineData("wrong.jpg","image/jpeg",false)]
    [InlineData("large.png","image/png",true)]
    public async Task Images_RejectInvalidFileWithoutMetadata(string name,string type,bool large)
    {
        using var client = api.NewClient(); var account = await api.RegisterAsync(client);
        using var create = await SendAsync(client,HttpMethod.Post,"/api/v1/recipes",api.RecipeBody(),account.AccessToken);
        var id = (await DataAsync(create,HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var bytes = large ? new byte[5*1024*1024+1] : name == "fake.png" ? "fake-image"u8.ToArray() : Png;
        using var upload = await Upload(client,id,account.AccessToken,bytes,name,type);
        Assert.Equal(HttpStatusCode.BadRequest,upload.StatusCode);
        Assert.Equal("application/problem+json",upload.Content.Headers.ContentType?.MediaType);
        await api.InScopeAsync(async services => Assert.False(await services.GetRequiredService<CulinaryBlogDbContext>().RecipeImages.AnyAsync(image => image.RecipeId == id)));
    }

    [Week8Fact]
    public async Task Images_AnotherAuthorCannotUpload()
    {
        using var client = api.NewClient(); var owner = await api.RegisterAsync(client); var other = await api.RegisterAsync(client);
        using var create = await SendAsync(client,HttpMethod.Post,"/api/v1/recipes",api.RecipeBody(),owner.AccessToken);
        var id = (await DataAsync(create,HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using var denied = await Upload(client,id,other.AccessToken,Png,"dish.png","image/png");
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var legacyDenied = await SendAsync(client,HttpMethod.Post,$"/api/v1/recipes/{id}/images",
            new { imageUrl = "https://example.test/image.png" },other.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,legacyDenied.StatusCode);
    }

    [Week8Fact]
    public async Task Users_AdminOnly_FilterSearchPagination_NoSensitiveFields()
    {
        using var client = api.NewClient(); var admin = await api.RegisterAsync(client);
        using var anonymous = await client.GetAsync("/api/v1/users"); Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        using var author = await SendAsync(client,HttpMethod.Get,"/api/v1/users",token:admin.AccessToken); Assert.Equal(HttpStatusCode.Forbidden,author.StatusCode);
        var prefix = "directory" + Guid.NewGuid().ToString("N");
        await api.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await manager.AddToRoleAsync((await manager.FindByIdAsync(admin.UserId))!,"Admin")).Succeeded);
            var db = services.GetRequiredService<CulinaryBlogDbContext>();
            var role = await db.Roles.SingleAsync(value => value.Name == "Author");
            for(var i=0;i<16;i++)
            {
                var user = new ApplicationUser { UserName=$"{prefix}{i}", Email=$"{prefix}{i}@example.test", DisplayName=$"{prefix} {i}" };
                db.Users.Add(user); db.UserRoles.Add(new IdentityUserRole<string> { UserId=user.Id,RoleId=role.Id });
            }
            await db.SaveChangesAsync();
        });
        admin = await LoginAsync(client,admin.Email);
        using var page1 = await SendAsync(client,HttpMethod.Get,$"/api/v1/users?role=Author&q={prefix}&pageSize=12",token:admin.AccessToken);
        var json = await page1.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(HttpStatusCode.OK,page1.StatusCode);
        Assert.Equal(16,json.GetProperty("meta").GetProperty("total").GetInt32());
        Assert.Equal(12,json.GetProperty("data").GetArrayLength());
        var ids = json.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToArray();
        foreach(var user in json.GetProperty("data").EnumerateArray())
        {
            Assert.False(user.TryGetProperty("passwordHash",out _)); Assert.False(user.TryGetProperty("securityStamp",out _)); Assert.False(user.TryGetProperty("refreshTokens",out _));
        }
        using var page2 = await SendAsync(client,HttpMethod.Get,$"/api/v1/users?role=Author&q={prefix}&pageSize=12&page=2",token:admin.AccessToken);
        var rest = (await DataAsync(page2)).EnumerateArray().ToArray(); Assert.Equal(4,rest.Length);
        Assert.DoesNotContain(rest,item => ids.Contains(item.GetProperty("id").GetString()));
        using var bad = await SendAsync(client,HttpMethod.Get,"/api/v1/users?role=Root&pageSize=100",token:admin.AccessToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,bad.StatusCode);
    }

    [Week8Fact]
    public async Task ArchiveAndMine_RespectOwnerAndStateFilters()
    {
        using var client = api.NewClient(); var owner = await api.RegisterAsync(client); var other = await api.RegisterAsync(client);
        using var create = await SendAsync(client,HttpMethod.Post,"/api/v1/recipes",api.RecipeBody(),owner.AccessToken);
        var draft = await DataAsync(create,HttpStatusCode.Created); var id = draft.GetProperty("id").GetGuid();
        using var publish = await SendAsync(client,HttpMethod.Patch,$"/api/v1/recipes/{id}/publish",token:owner.AccessToken); await DataAsync(publish);
        using var denied = await SendAsync(client,HttpMethod.Patch,$"/api/v1/recipes/{id}/archive",token:other.AccessToken); Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var archive = await SendAsync(client,HttpMethod.Patch,$"/api/v1/recipes/{id}/archive",token:owner.AccessToken);
        Assert.Equal("Archived",(await DataAsync(archive)).GetProperty("status").GetString());
        using var hidden = await client.GetAsync($"/api/v1/recipes/{id}"); Assert.Equal(HttpStatusCode.Forbidden,hidden.StatusCode);
        using var mine = await SendAsync(client,HttpMethod.Get,"/api/v1/recipes?mine=true&status=Archived",token:owner.AccessToken);
        Assert.Contains((await DataAsync(mine)).EnumerateArray(),item => item.GetProperty("id").GetGuid()==id);
        using var others = await SendAsync(client,HttpMethod.Get,"/api/v1/recipes?mine=true&status=Archived",token:other.AccessToken);
        Assert.DoesNotContain((await DataAsync(others)).EnumerateArray(),item => item.GetProperty("id").GetGuid()==id);
        using var anonymousMine = await client.GetAsync("/api/v1/recipes?mine=true"); Assert.Equal(HttpStatusCode.Unauthorized,anonymousMine.StatusCode);
        using var delete = await SendAsync(client,HttpMethod.Delete,$"/api/v1/recipes/{id}",token:owner.AccessToken); Assert.Equal(HttpStatusCode.NoContent,delete.StatusCode);
    }

    [Week8Fact]
    public async Task Logging_RecordsCorrelationUserStatusAndElapsed_WithoutTokensOrPassword()
    {
        using var client = api.NewClient(); var account = await api.RegisterAsync(client);
        var correlation = "lab8-" + Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Correlation-Id",correlation);
        using var create = await SendAsync(client,HttpMethod.Post,"/api/v1/recipes",api.RecipeBody(),account.AccessToken);
        await DataAsync(create,HttpStatusCode.Created);
        await Task.Delay(100);
        var lines = new List<string>();
        foreach (var path in Directory.GetFiles(api.LogDirectory,"*.jsonl"))
        {
            using var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            using var reader = new StreamReader(stream); lines.AddRange((await reader.ReadToEndAsync()).Split('\n',StringSplitOptions.RemoveEmptyEntries));
        }
        var records = lines.Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
        Assert.Contains(records,record => record.TryGetProperty("Properties",out var fields)
            && fields.TryGetProperty("RequestPath",out var path) && path.GetString()=="/api/v1/recipes"
            && fields.TryGetProperty("CorrelationId",out var cid) && cid.GetString()==correlation
            && fields.TryGetProperty("UserId",out var uid) && uid.GetString()==account.UserId
            && fields.TryGetProperty("StatusCode",out var code) && code.GetInt32()==201 && fields.TryGetProperty("Elapsed",out _));
        var text = string.Join('\n',lines);
        Assert.DoesNotContain(account.AccessToken,text); Assert.DoesNotContain(account.RefreshToken,text); Assert.DoesNotContain(Password,text);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client,Guid id,string token,byte[] bytes,string name,string type)
    {
        using var body = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes); content.Headers.ContentType = new MediaTypeHeaderValue(type); body.Add(content,"file",name);
        using var request = new HttpRequestMessage(HttpMethod.Post,$"/api/v1/recipes/{id}/images") { Content=body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);
        return await client.SendAsync(request);
    }
}
