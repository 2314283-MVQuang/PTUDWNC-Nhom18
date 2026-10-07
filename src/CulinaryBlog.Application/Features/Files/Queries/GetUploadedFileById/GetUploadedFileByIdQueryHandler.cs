using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Files.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Files.Queries.GetUploadedFileById;

public class GetUploadedFileByIdQueryHandler(IUploadedFileRepository uploadedFileRepository)
    : IRequestHandler<GetUploadedFileByIdQuery, UploadedFileDto>
{
    public async Task<UploadedFileDto> Handle(GetUploadedFileByIdQuery request, CancellationToken cancellationToken)
    {
        var file = await uploadedFileRepository.GetByIdAsync(request.Id, cancellationToken);
        if (file == null)
        {
            throw new NotFoundException("UploadedFile", request.Id);
        }

        return new UploadedFileDto(
            Id: file.Id,
            FileName: file.FileName,
            ContentType: file.ContentType,
            Size: file.Size,
            StorageKey: file.StorageKey,
            Url: file.Url,
            BucketName: file.BucketName,
            UploadedBy: file.UploadedBy,
            CreatedAt: file.CreatedAt
        );
    }
}
