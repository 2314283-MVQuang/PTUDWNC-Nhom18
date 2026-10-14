namespace CulinaryBlog.Domain.Common;

/// <summary>
/// Lớp cha cho mọi entity trong hệ thống (mục 7.1 tài liệu đặc tả).
///
/// Ba cột cuối được QUẢN LÝ BỞI DATABASE, không phải bởi code C#:
///   - CreatedAt / UpdatedAt: AuditInterceptor (xem Infrastructure/Persistence/Interceptors)
///     set khi SaveChanges, nhưng PostgreSQL cũng có trigger "touch_row()" (xem
///     db/init/02-schema.sql) tự cập nhật UpdatedAt mỗi lần UPDATE — hai lớp bảo vệ này
///     không xung đột nhau.
///   - RowVersion: KHÔNG bao giờ set tay. Trigger "touch_row()" sinh giá trị ngẫu nhiên mới
///     mỗi lần UPDATE, EF Core đọc lại giá trị đó sau khi lưu (xem cấu hình IsRowVersion()
///     trong các file *Configuration.cs) để dùng cho optimistic concurrency (If-Match header).
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// KHÔNG gán sẵn Guid.NewGuid() ở đây. EF Core quyết định một entity tìm thấy qua navigation
    /// là hàng MỚI hay hàng CŨ dựa trên việc khoá chính đã có giá trị hay chưa; gán sẵn Id khiến
    /// "recipe.Ingredients.Add(...)" bị hiểu là Modified rồi sinh ra UPDATE thay vì INSERT.
    /// Để trống thì EF tự sinh Guid lúc Add (cột trong DB cũng có DEFAULT gen_random_uuid()).
    /// </summary>
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Soft-delete flag. DeletedAt records when the entity was marked deleted.</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
