using CulinaryBlog.Application.Features.Files.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Files.Commands.GeneratePresignedUrl;

/// <summary>
/// Yêu cầu cấp Presigned URL để upload file lên MinIO kèm lưu metadata vào CSDL.
/// </summary>
public record GeneratePresignedUrlCommand(
    string FileName,
    string ContentType,
    long Size
) : IRequest<PresignedUploadUrlDto>;
