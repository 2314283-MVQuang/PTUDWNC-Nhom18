using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Common.Behaviors;

/// <summary>
/// Pipeline Behavior #1 (mục 6.3): log loại request, thời gian xử lý, cảnh báo nếu > 500ms.
/// Áp dụng cho TẤT CẢ Commands và Queries vì được đăng ký làm open generic trong DependencyInjection.cs.
///
/// TODO (nhóm làm tiếp): thêm CorrelationId/UserId vào log scope khi tích hợp Serilog +
/// CorrelationIdMiddleware (FR-OBS-002) — hiện dùng ILogger mặc định của .NET.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Bắt đầu xử lý {RequestName}", requestName);

        TResponse response;
        try { response = await next(); }
        catch (Exception error)
        {
            logger.LogWarning("{RequestName} thất bại sau {ElapsedMilliseconds}ms: {ExceptionType}",
                requestName, stopwatch.ElapsedMilliseconds, error.GetType().Name);
            throw;
        }

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            logger.LogWarning(
                "{RequestName} xử lý CHẬM: {ElapsedMilliseconds}ms (ngưỡng {Threshold}ms)",
                requestName, stopwatch.ElapsedMilliseconds, SlowRequestThresholdMs);
        }
        else
        {
            logger.LogInformation(
                "Hoàn tất {RequestName} trong {ElapsedMilliseconds}ms",
                requestName, stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}
