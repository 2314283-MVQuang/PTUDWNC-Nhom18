using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.UnitTests.Api;

public class GoogleConfigurationTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingAudience_RejectsGoogleLoginBeforeTokenValidation(string clientId)
    {
        var service = new GoogleAuthService(Options.Create(new GoogleOptions { ClientId = clientId }));
        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => service.ValidateIdTokenAsync("not-a-token"));
        Assert.Contains("chưa được cấu hình", error.Message);
    }
}
