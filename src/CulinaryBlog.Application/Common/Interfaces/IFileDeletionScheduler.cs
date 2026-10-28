namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// FR-FILE-002 (SRS trang 38, FR-RCP-008 bước 14): xoá file trên MinIO bằng Hangfire job
/// fire-and-forget, gọi SAU khi dữ liệu đã lưu xong. MinIO lỗi kết nối thì Hangfire tự thử lại tối
/// đa 3 lần — thao tác của người dùng (xoá ảnh, purge công thức) không bị hỏng theo.
/// </summary>
public interface IFileDeletionScheduler
{
    /// <summary>Enqueue 1 job xoá cho mỗi object name (key trong bucket). Trả về số job đã tạo.</summary>
    int Enqueue(IEnumerable<string> objectNames);
}
