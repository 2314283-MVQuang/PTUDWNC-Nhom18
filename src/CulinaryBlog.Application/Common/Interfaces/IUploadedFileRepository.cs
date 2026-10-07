using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// Repository quản lý truy xuất và lưu trữ metadata cho tệp tin tải lên (UploadedFile).
/// </summary>
public interface IUploadedFileRepository
{
    Task<UploadedFile> AddAsync(UploadedFile file, CancellationToken ct = default);

    Task<UploadedFile?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<UploadedFile?> GetByStorageKeyAsync(string storageKey, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
