using CulinaryBlog.Application.Features.Files.Commands.DeleteFile;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-FILE-002 (Thịnh): Hangfire job xoá 1 object khỏi MinIO qua <see cref="DeleteFileCommand"/>.
/// SRS trang 38: object không tồn tại → không lỗi (RemoveObject của MinIO vốn idempotent); lỗi kết
/// nối MinIO → Hangfire thử lại tối đa 3 lần rồi chuyển Failed (xem trên /hangfire).
/// </summary>
public sealed class DeleteFileJob(ISender sender, ILogger<DeleteFileJob> logger)
{
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(string objectName, CancellationToken ct = default)
    {
        await sender.Send(new DeleteFileCommand(objectName), ct);
        logger.LogInformation("DeleteFileJob: đã xoá object {ObjectName} khỏi MinIO.", objectName);
    }
}
