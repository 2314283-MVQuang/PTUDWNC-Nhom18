using CulinaryBlog.Application.Features.Files.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Files.Queries.GetUploadedFileById;

/// <summary>
/// Lấy thông tin metadata và đường dẫn của tệp tin theo ID (GET /api/v1/files/{id}).
/// </summary>
public record GetUploadedFileByIdQuery(Guid Id) : IRequest<UploadedFileDto>;
