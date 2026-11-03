using System.Diagnostics;
using System.Security.Claims;
using Serilog;
using Serilog.Events;

namespace CulinaryBlog.API.Extensions;

public static class SystemLoggingExtensions
{
    public static IApplicationBuilder UseSystemRequestLogging(this IApplicationBuilder app) => app.UseSerilogRequestLogging(options =>
    {
        options.Logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} returned {StatusCode} in {Elapsed:0.000} ms";
        options.GetLevel = (context, elapsed, error) => error is not null || context.Response.StatusCode >= 500
            ? LogEventLevel.Error : elapsed > 500 || context.Response.StatusCode >= 400 ? LogEventLevel.Warning : LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagnostic, context) =>
        {
            diagnostic.Set("UserId", context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous");
            diagnostic.Set("CorrelationId", context.Response.Headers["X-Correlation-Id"].ToString());
            diagnostic.Set("TraceId", Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);
        };
    });
}
