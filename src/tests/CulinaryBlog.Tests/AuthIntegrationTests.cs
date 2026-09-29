using System.Net.Http.Json;
using System.Threading.Tasks;
using CulinaryBlog.API;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FluentAssertions;

namespace CulinaryBlog.Tests;

public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Create client with test server
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ShouldReturnCreated_WhenValidRequest()
    {
        // Arrange
        var registerDto = new
        {
            Email = "testuser@example.com",
            Password = "Password123!",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/register", registerDto);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<User>();
        result?.Email.Should().Be(registerDto.Email);
    }

    [Fact]
    public async Task Login_ShouldReturnOk_WithValidCredentials()
    {
        // First register a user
        var registerDto = new
        {
            Email = "loginuser@example.com",
            Password = "Password123!",
            FirstName = "Login",
            LastName = "User"
        };
        await _client.PostAsJsonAsync("/auth/register", registerDto);

        // Arrange login request
        var loginDto = new
        {
            Email = registerDto.Email,
            Password = registerDto.Password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", loginDto);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<dynamic>();
        ((string)tokenResponse?.accessToken).Should().NotBeNullOrEmpty();
    }
}
