using System.Net;
using CulinaryBlog.API.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CulinaryBlog.UnitTests.Api;

/// <summary>Tuần 10 (Quang) — chạy sau Caddy trên máy chủ thật: rate limit phải thấy IP thật của người dùng.</summary>
public sealed class ReverseProxyHeadersTests
{
    private static async Task<HttpContext> RunAsync(string peerIp, string? forwardedFor, string? proto = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peerIp);
        context.Request.Scheme = "http";
        if (forwardedFor is not null) context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        if (proto is not null) context.Request.Headers["X-Forwarded-Proto"] = proto;

        await new ReverseProxyHeadersMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
        return context;
    }

    [Fact]
    public async Task FromDockerNetwork_UsesLastForwardedIp_AndHttpsScheme()
    {
        var context = await RunAsync("172.18.0.5", "1.2.3.4, 113.161.10.20", "https");

        Assert.Equal(IPAddress.Parse("113.161.10.20"), context.Connection.RemoteIpAddress);
        Assert.Equal("https", context.Request.Scheme);
    }

    [Fact]
    public async Task FromInternet_IgnoresSpoofedHeaders()
    {
        var context = await RunAsync("113.161.10.20", "10.0.0.1", "https");

        Assert.Equal(IPAddress.Parse("113.161.10.20"), context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Fact]
    public async Task InvalidForwardedValue_KeepsPeerAddress()
    {
        var context = await RunAsync("172.18.0.5", "not-an-ip", "ftp");

        Assert.Equal(IPAddress.Parse("172.18.0.5"), context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.20.30.40", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("192.168.1.10", true)]
    [InlineData("::ffff:172.18.0.2", true)]
    [InlineData("fd00::1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("2001:4860::8888", false)]
    public void IsPrivateNetwork_MatchesDockerRanges(string ip, bool expected)
    {
        Assert.Equal(expected, ReverseProxyHeadersMiddleware.IsPrivateNetwork(IPAddress.Parse(ip)));
    }
}
