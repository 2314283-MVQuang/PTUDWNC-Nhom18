using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog;
using Xunit;

namespace CulinaryBlog.UnitTests.Api;

public class LoggingConfigurationTests
{
    [Fact]
    public void ProductionSettings_WriteJsonDailyLogWithoutSeqDependency()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,"CulinaryBlog.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(Path.GetTempPath(),"culinary-logging-config-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(root!.FullName,"src/CulinaryBlog.API/appsettings.json"))
            .AddInMemoryCollection(new Dictionary<string,string?> { ["Serilog:WriteTo:1:Args:path"] = Path.Combine(directory,"system-.jsonl") }).Build();
        Assert.DoesNotContain(configuration.GetSection("Serilog:WriteTo").GetChildren(), sink => sink["Name"] == "Seq");
        using (var logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger())
            logger.Information("Logging configuration verified {Check}","Lab8");
        var file = Assert.Single(Directory.GetFiles(directory,"system-*.jsonl"));
        using var parsed = JsonDocument.Parse(File.ReadAllText(file));
        Assert.Equal("Lab8",parsed.RootElement.GetProperty("Properties").GetProperty("Check").GetString());
    }
}
