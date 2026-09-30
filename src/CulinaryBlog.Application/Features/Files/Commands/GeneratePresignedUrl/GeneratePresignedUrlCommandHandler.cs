using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Files.Dtos;
using CulinaryBlog.Domain.Entities;
using MediatR;

namespace CulinaryBlog.Application.Features.Files.Commands.GeneratePresignedUrl;

public class GeneratePresignedUrlCommandHandler(
    IFileStorageService fileStorageService,
    IUploadedFileRepository uploadedFileRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GeneratePresignedUrlCommand, PresignedUploadUrlDto>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private const int PresignedUrlExpirySeconds = 900; // 15 phút

    public async Task<PresignedUploadUrlDto> Handle(GeneratePresignedUrlCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new InvalidFileException("Tên tệp tin không được để trống.");
        }

        if (request.Size <= 0)
        {
            throw new InvalidFileException("Kích thước tệp tin phải lớn hơn 0 bytes.");
        }

        if (request.Size > MaxFileSizeBytes)
        {
            throw new InvalidFileException($"Kích thước tệp ({request.Size / 1024 / 1024.0:F2} MB) vượt quá giới hạn cho phép tối đa 10 MB.");
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidFileException($"Định dạng mở rộng '{extension}' không được hỗ trợ. Chỉ cho phép các định dạng: {string.Join(", ", AllowedExtensions)}.");
        }

        var normalizedContentType = request.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedContentTypes.Contains(normalizedContentType))
        {
            throw new InvalidFileException($"Loại nội dung (Content-Type) '{request.ContentType}' không hợp lệ. Chỉ chấp nhận các loại ảnh: {string.Join(", ", AllowedContentTypes)}.");
        }

        var sanitizedFileName = Path.GetFileName(request.FileName);
        var storageKey = $"uploads/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}_{sanitizedFileName}";

        var uploadUrl = await fileStorageService.GeneratePresignedUploadUrlAsync(
            storageKey,
            normalizedContentType,
            PresignedUrlExpirySeconds,
            cancellationToken);

        var publicUrl = fileStorageService.GetPublicUrl(storageKey);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(PresignedUrlExpirySeconds);

        var uploadedFile = new UploadedFile
        {
            FileName = sanitizedFileName,
            ContentType = normalizedContentType,
            Size = request.Size,
            StorageKey = storageKey,
            Url = publicUrl,
            BucketName = fileStorageService.BucketName,
            UploadedBy = currentUser.UserId,
        };

        var savedFile = await uploadedFileRepository.AddAsync(uploadedFile, cancellationToken);

        return new PresignedUploadUrlDto(
            FileId: savedFile.Id,
            UploadUrl: uploadUrl,
            PublicUrl: publicUrl,
            StorageKey: storageKey,
            FileName: sanitizedFileName,
            ContentType: normalizedContentType,
            Size: request.Size,
            ExpiresAt: expiresAt
        );
    }
}
