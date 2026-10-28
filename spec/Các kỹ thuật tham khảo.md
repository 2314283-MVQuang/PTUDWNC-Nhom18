<a name="chuong-2"></a>
## CHƯƠNG 2: THIẾT KẾ RESTFUL API & ASP.NET 10 MINIMAL APIS

### 2.1 Nguyên Lý Thiết Kế RESTful API

#### 1. Ràng Buộc Kiến Trúc REST (Roy Fielding, 2000)
1. **Client-Server**: Tách biệt giao diện người dùng và hạ tầng lưu trữ.
2. **Stateless**: Mỗi request từ Client lên Server phải chứa đầy đủ thông tin để hiểu request đó. Server không lưu session client trong bộ nhớ.
3. **Cacheable**: Response phải khai báo rõ có thể cache hay không.
4. **Uniform Interface**: Giao diện thống nhất qua Resource URIs, HTTP Methods, Standard Status Codes.
5. **Layered System**: Client không cần biết đang gọi trực tiếp Server hay thông qua CDN/Gateway/Proxy.
6. **Code on Demand (Optional)**: Server có thể gửi code thực thi về client (ít dùng).

#### 2. Richardson Maturity Model
* **Level 0 (The Swamp of POX)**: 1 URI duy nhất, luôn dùng `POST` (mô hình RPC qua HTTP).
* **Level 1 (Resources)**: Nhiều URIs riêng cho từng loại tài nguyên, nhưng vẫn dùng 1 method `POST` hoặc `GET`.
* **Level 2 (HTTP Verbs)**: Sử dụng chính xác HTTP Methods (`GET`, `POST`, `PUT`, `PATCH`, `DELETE`) và HTTP Status Codes chuẩn.
* **Level 3 (HATEOAS)**: Hypermedia As The Engine Of Application State — Response trả về kèm các đường dẫn liên kết (`_links`) điều hướng các hành động tiếp theo.

#### 3. Quy Ước Thiết Kế Resource & URI Scheme
* **Chỉ dùng Danh từ số nhiều**, không dùng động từ trên URI:
  * ❌ `GET /api/getCategories`, `POST /api/createRecipe`
  * ✅ `GET /api/v1/categories`, `POST /api/v1/recipes`
* **Quan hệ lồng nhau (Nested Resources)**: `GET /api/v1/categories/{categoryId}/recipes`
* **Query Parameters cho Lọc, Sắp Xếp, Phân Trang**: `GET /api/v1/recipes?page=2&pageSize=10&sortBy=createdAt&descending=true`

---

### 2.2 HTTP Methods, Status Codes & Idempotency

| Method | Ngữ Nghĩa | Safe | Idempotent | Request Body | Status Code Mặc Định |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **GET** | Đọc dữ liệu | ✅ | ✅ | ❌ | `200 OK` |
| **POST** | Tạo tài nguyên mới | ❌ | ❌ | ✅ | `201 Created` (kèm `Location` header) |
| **PUT** | Thay thế toàn bộ tài nguyên | ❌ | ✅ | ✅ | `200 OK` hoặc `204 No Content` |
| **PATCH** | Cập nhật một phần tài nguyên | ❌ | ❌* | ✅ | `200 OK` |
| **DELETE**| Xóa tài nguyên | ❌ | ✅ | ❌ | `204 No Content` hoặc `200 OK` |

*> Note: Idempotency (Lũy đẳng) nghĩa là việc thực thi request 1 lần hay N lần liên tiếp cho ra cùng một trạng thái hệ thống.*

#### Nhóm HTTP Status Codes Chuẩn
* **2xx (Thành công)**: `200 OK`, `201 Created`, `204 No Content`.
* **3xx (Chuyển hướng)**: `301 Moved Permanently`, `304 Not Modified`.
* **4xx (Lỗi Client)**:
  * `400 Bad Request`: Format sai, vi phạm syntax.
  * `401 Unauthorized`: Chưa đăng nhập / Token không hợp lệ.
  * `403 Forbidden`: Đã đăng nhập nhưng không đủ quyền.
  * `404 Not Found`: Tài nguyên không tồn tại.
  * `409 Conflict`: Xung đột dữ liệu (ví dụ: trùng Unique Slug/Email).
  * `422 Unprocessable Entity`: Dữ liệu đúng syntax nhưng vi phạm Validation / Business Rules.
  * `429 Too Many Requests`: Vượt quá Rate Limit.
* **5xx (Lỗi Server)**: `500 Internal Server Error`, `503 Service Unavailable`.

#### Phân Trở Lỗi Chuẩn RFC 7807 (ProblemDetails)
```json
{
  "type": "rfc7231/section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Please refer to the errors property for additional details.",
  "instance": "/api/v1/categories",
  "errors": {
    "Name": ["The Name field is required."]
  }
}
```

---

### 2.3 Cấu Trúc Phân Trang Chuẩn (PaginatedResult)

```csharp
public sealed class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public PaginatedResult(IReadOnlyList<T> items, int count, int page, int pageSize)
    {
        Items = items;
        TotalCount = count;
        Page = page;
        PageSize = pageSize;
    }
}
```

---

### 2.4 Khai Báo Endpoints Trong ASP.NET 10 Minimal APIs

#### Bổ Sung Mới Trong .NET 10 Minimal APIs:
1. **Validation Tích Hợp**: `builder.Services.AddValidation()` kết hợp DataAnnotations trên Record.
2. **OpenAPI 3.1 & Scalar Integration**: Thay thế Swagger UI mặc định bằng Scalar API Reference gọn gàng, hiện đại.
3. **TypedResults**: Đảm bảo type-safety compile-time và tự động trích xuất OpenAPI responses.
4. **Server-Sent Events (SSE)**: `TypedResults.ServerSentEvents(stream)` truyền dữ liệu realtime 1 chiều.

#### Ví Dụ Route Group Chuẩn Trong Minimal APIs:
```csharp
public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories")
                       .WithTags("Categories")
                       .WithOpenApi();

        group.MapGet("/", async ([AsParameters] GetCategoriesQuery query, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(query, ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetCategories")
        .WithSummary("Lấy danh sách danh mục có phân trang, lọc và sắp xếp");

        group.MapPost("/", async (CreateCategoryCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return TypedResults.Created($"/api/v1/categories/{result.Id}", result);
        })
        .WithName("CreateCategory")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
```
<a name="chuong-3"></a>
## CHƯƠNG 3: THIẾT KẾ DỮ LIỆU & ENTITY FRAMEWORK CORE (BASIC & ADVANCED)

### 3.1 Active Record Vs Data Mapper & EF Core Architecture

* **Active Record (e.g., Ruby on Rails, Django ORM)**: Entity Class chứa cả Data Properties lẫn Database Operations (`recipe.Save()`, `Recipe.Find(1)`). Coupling cao giữa Domain và Database.
* **Data Mapper (e.g., EF Core, NHibernate)**: Entity hoàn toàn là POCO (Plain Old C# Object). Việc ánh xạ và ghi đè xuống Database do một lớp trung gian đảm nhận (`DbContext` & `DbSet<T>`). Phù hợp chuẩn mực Clean Architecture.
* **Change Tracking trong EF Core**: `DbContext` duy trì 1 Snapshot của các entity được load từ DB. Khi gọi `SaveChangesAsync()`, EF Core so sánh trạng thái hiện tại với Snapshot để tạo ra câu lệnh SQL `UPDATE` tối ưu (chỉ update các cột bị thay đổi).

---

### 3.2 Fluent API & IEntityTypeConfiguration<T>

Thay vì dùng DataAnnotations (như `[Required]`, `[MaxLength]`) làm vẩn đục Entity trong Domain Layer, toàn bộ cấu hình DB được thực hiện bằng **Fluent API** trong Infrastructure Layer.

```csharp
public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(r => r.Slug)
               .IsRequired()
               .HasMaxLength(220);

        // Value Conversion: Lưu Enum dưới dạng String trong DB
        builder.Property(r => r.Difficulty)
               .HasConversion<string>()
               .HasMaxLength(20);

        // Owned Entity (Value Object): Lưu cùng bảng Recipes
        builder.OwnsOne(r => r.Nutrition, n =>
        {
            n.Property(x => x.Calories).HasColumnName("Nutrition_Calories").HasColumnType("decimal(8,2)");
            n.Property(x => x.Protein).HasColumnName("Nutrition_Protein").HasColumnType("decimal(6,2)");
        });

        // Concurrency Token (Optimistic Locking)
        builder.Property(r => r.RowVersion)
               .IsRowVersion()
               .IsConcurrencyToken();

        // Relationships
        builder.HasOne(r => r.Category)
               .WithMany(c => c.Recipes)
               .HasForeignKey(r => r.CategoryId)
               .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(r => r.Slug).IsUnique();
        builder.HasIndex(r => r.CategoryId);
    }
}
```

---

### 3.3 Các Kỹ Thuật Tối Ưu Truy Vấn Trong EF Core

#### 1. N+1 Query Problem Và Cách Khắc Phục
* **Vấn đề N+1**: Khi query N đối tượng và truy cập navigation property trong vòng lặp mà không Load trước, EF Core sẽ bắn thêm N câu SQL query lẻ -> Tổng 1 + N queries dội vào Database.
* **Khắc phục**:
  * **Eager Loading**: Dùng `.Include()` và `.ThenInclude()`.
  * **Projection**: Dùng `.Select()` hoặc Mapster `.ProjectToType<TDto>()` để SQL chỉ lấy đúng các cột DTO cần.

```csharp
// ❌ SAI: Gây N+1 Queries
var recipes = await context.Recipes.ToListAsync();
foreach (var r in recipes)
    Console.WriteLine(r.Category.Name); // Mỗi lần chạy bắn 1 SQL query!

// ✅ ĐÚNG: Eager Loading (1 Query JOIN duy nhất)
var recipes = await context.Recipes
    .Include(r => r.Category)
    .AsNoTracking()
    .ToListAsync();

// ✅ ĐÚNG NHẤT: Projection (Chỉ SELECT các cột có trong RecipeListDto)
var recipes = await context.Recipes
    .AsNoTracking()
    .ProjectToType<RecipeListDto>()
    .ToListAsync();
```

#### 2. Cartesian Explosion Và `AsSplitQuery()`
Khi `.Include()` nhiều Navigation Collections (1-N, N-N) trong cùng một query, SQL JOIN tạo ra tích Descartes (Cartesian Product) khiến số lượng dòng trả về bùng nổ, nhân bản dữ liệu trên đường truyền.
* **Giải pháp**: Sử dụng `.AsSplitQuery()` để EF Core tách thành các câu lệnh SQL `SELECT` riêng biệt cho từng collection.

```csharp
var recipe = await context.Recipes
    .AsSplitQuery()
    .Include(r => r.Category)
    .Include(r => r.Steps.OrderBy(s => s.StepNumber))
    .Include(r => r.Ingredients)
    .FirstOrDefaultAsync(r => r.Id == id);
```

#### 3. `AsNoTracking()`
Khi truy vấn chỉ đọc (Read-only), gọi `.AsNoTracking()` để yêu cầu EF Core tắt cơ chế Change Tracker. Giúp tiết kiệm 30% - 50% bộ nhớ RAM và CPU của Server.

#### 4. Global Query Filters & Soft Delete
```csharp
// Cấu hình Soft Delete tự động lọc bản ghi bị xóa tạm
builder.HasQueryFilter(r => !r.IsDeleted);

// Khi Admin muốn xem cả bản ghi đã bị xóa:
var allRecipes = await context.Recipes.IgnoreQueryFilters().ToListAsync();
```

#### 5. Bulk Operations Trong EF Core 7+ (`ExecuteUpdate` / `ExecuteDelete`)
Thực hiện UPDATE/DELETE hàng loạt trực tiếp trên Database mà không cần nạp Entity vào bộ nhớ theo cách truyền thống.

```csharp
// Update giá trị trực tiếp trên DB không nạp entity
await context.Recipes
    .Where(r => r.CategoryId == categoryId)
    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RecipeStatus.Archived));
```

---

### 3.4 PostgreSQL Full-Text Search (FTS) Với EF Core

PostgreSQL hỗ trợ tìm kiếm toàn văn mạnh mẽ thông qua kiểu dữ liệu `tsvector` và `tsquery`.

#### 1. Cấu hình Computed Column & GIN Index trong EF Core
```csharp
// Computed Column tự động ghép Title + Description không dấu
builder.Property(r => r.SearchVector)
       .HasComputedColumnSql(
           "to_tsvector('simple', unaccent(coalesce("Title", '')) || ' ' || unaccent(coalesce("Description", '')))",
           stored: true);

// GIN Index giúp tra cứu O(log N)
builder.HasIndex(r => r.SearchVector).HasMethod("GIN");
```

#### 2. Viết Query Full-Text Search
```csharp
var tsQuery = EF.Functions.PlainToTsQuery("simple", searchTerm);

var results = await context.Recipes
    .AsNoTracking()
    .Where(r => r.SearchVector.Matches(tsQuery))
    .OrderByDescending(r => r.SearchVector.Rank(tsQuery))
    .ProjectToType<RecipeListDto>()
    .ToListAsync();
```
<a name="chuong-4"></a>
## CHƯƠNG 4: TRỪU TƯỜNG HÓA TẦNG DỮ LIỆU: REPOSITORY PATTERN & UNIT OF WORK

### 4.1 Khái Niệm & Sự Khác Biệt Giữa Các Pattern

#### 1. Repository Pattern
* **Mục đích**: Che giấu chi tiết truy vấn/lưu trữ dữ liệu đằng sau một Interface hướng Domain, tạo cho Application Layer cảm giác như đang thao tác với một In-memory Collection.
* **Generic Repository**: `IRepository<T>` xử lý CRUD cơ bản.
* **Specific Repository**: `IRecipeRepository` kế thừa `IRepository<Recipe>` và bổ sung các hàm đặc thù của Aggregate.

#### 2. Unit of Work Pattern
* **Mục đích**: Quản lý một Transaction nghiệp vụ. Đảm bảo tất cả các thao tác thêm, sửa, xóa qua nhiều Repositories khác nhau được Commit xuống Database trong **MỘT TRANSACTION DUY NHẤT** (`SaveChangesAsync`).

```csharp
public interface IUnitOfWork : IAsyncDisposable
{
    IRecipeRepository Recipes { get; }
    IRepository<Category> Categories { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
}
```

---

### 4.2 Cuộc Tranh Luận: DbContext Trực Tiếp Vs Repository + Unit Of Work

| Tiêu Chí | Repository + Unit of Work (Thủ công) | DbContext Trực Tiếp (`IApplicationDbContext`) |
| :--- | :--- | :--- |
| **Bản chất trong EF Core** | Bọc thêm 1 lớp Abstraction xung quanh `DbContext`. | Bản thân `DbContext` đã LÀ một **Unit of Work**, còn `DbSet<T>` đã LÀ một **Repository**. |
| **Độ phức tạp** | Cao hơn, phát sinh nhiều Interface & Class trung gian. | Thấp hơn, tận dụng tối đa sức mạnh LINQ/EF Core. |
| **Rò rỉ Abstraction** | Dễ rò rỉ nếu expose `IQueryable<T>` ra ngoài Repository. | Nhận thức rõ ràng việc đang sử dụng EF Core trong Infrastructure/Handlers. |
| **Khả năng Unit Test** | Dễ dàng Mock Interface `IRepository`. | Cần dùng SQLite In-Memory / Testcontainers hoặc Mock `DbSet`. |
| **Trường hợp khuyên dùng**| Hệ thống DDD phức tạp, ứng dụng có ĐA NGUỒN DỮ LIỆU (SQL + NoSQL + External API). | 90% dự án ASP.NET Core Clean Architecture tiêu chuẩn. |
<a name="chuong-5"></a>
## CHƯƠNG 5: TỐI ƯU HÓA TRUY VẤN CƠ SỞ DỮ LIỆU QUAN HỆ

### 5.1 Cấu Trúc Index Trong Relational Database

#### 1. Clustered Index vs Non-Clustered Index
* **Clustered Index**: Quyết định thứ tự tổ chức dữ liệu ĐĨA VẬT LÝ của bảng. Mỗi bảng chỉ có duy nhất **1 Clustered Index** (thường là Primary Key ID).
* **Non-Clustered Index**: Cấu trúc B-Tree riêng biệt chứa giá trị cột Index + Con trỏ (Row Locator / Primary Key) trỏ về dòng dữ liệu thực tế. Một bảng có thể có nhiều Non-Clustered Index.

#### 2. Covering Index (Sử dụng `INCLUDE`)
Cho phép gộp các cột phụ (không dùng làm điều kiện lọc `WHERE`) vào nút lá của Non-Clustered Index. Giúp Database Engine lấy toàn bộ dữ liệu ngay từ Index mà không tốn chi phí tốn kém **Key Lookup / Bookmark Lookup** về bảng gốc.

```sql
-- PostgreSQL / SQL Server Covering Index
CREATE INDEX IX_Recipes_CategoryId_Covering 
ON "Recipes" ("CategoryId") 
INCLUDE ("Title", "Slug", "CreatedAt");
```

#### 3. Composite Index & Nguyên Tắc Leftmost Prefix
* Composite Index phủ trên nhiều cột: `(ColumnA, ColumnB, ColumnC)`.
* **Nguyên tắc Leftmost Prefix**: Index chỉ có tác dụng Seek khi câu query chứa các cột từ TÁI SANG PHẢI liên tục bắt đầu từ cột đầu tiên (`ColumnA`, hoặc `ColumnA + ColumnB`). Nếu query chỉ lọc theo `ColumnB` hoặc `ColumnC`, Index sẽ bị bỏ qua (hoặc Index Scan).

---

### 5.2 Viết Câu Query Sargable (Search Argument Able)

Query Sargable là câu query cho phép Database Engine tận dụng **Index Seek** thay vì phải **Index Scan / Table Scan**.

| Loại Query | Non-Sargable (❌ Chậm - Quét Toàn Bảng) | Sargable (✅ Nhanh - Index Seek) |
| :--- | :--- | :--- |
| **Dùng hàm trên Cột** | `WHERE YEAR(CreatedAt) = 2026` | `WHERE CreatedAt >= '2026-01-01' AND CreatedAt < '2027-01-01'` |
| **Wildcard ở đầu** | `WHERE Title LIKE '%Phở%'` | `WHERE Title LIKE 'Phở%'` (Hoặc dùng Full-Text Search) |
| **Ép kiểu ngầm định** | `WHERE StringCol = 123` (N-Varchar vs Int) | `WHERE StringCol = '123'` |
| **Phép toán trên Cột**| `WHERE Price * 0.9 > 100` | `WHERE Price > 100 / 0.9` |

---

### 5.3 So Sánh Phân Trang: Offset-Based Vs Cursor-Based (Keyset)

#### 1. Offset-Based Pagination (`OFFSET N FETCH NEXT M`)
* **Cách hoạt động**: `SELECT * FROM Recipes ORDER BY CreatedAt DESC OFFSET 10000 ROWS FETCH NEXT 10 ROWS ONLY;`
* **Nhược điểm**: Trang càng sâu, DB càng chậm vì phải đọc và sắp xếp đếm bỏ qua 10,000 dòng trước đó.
* **Rủi ro**: Bị lặp dòng hoặc sót dòng nếu dữ liệu bị chèn/xóa trong lúc user chuyển trang.

#### 2. Cursor-Based / Keyset Pagination
* **Cách hoạt động**: Sử dụng Con trỏ (Cursor - dựa trên Cột Unique + Sắp xếp của bản ghi cuối trang trước) làm điều kiện `WHERE`.

```sql
-- Phân trang con trỏ lấy 10 bản ghi tiếp theo sau Id/CreatedAt cụ thể
SELECT TOP(10) Id, Title, CreatedAt 
FROM Recipes
WHERE (CreatedAt < '2026-10-20 10:00:00') 
   OR (CreatedAt = '2026-10-20 10:00:00' AND Id < 'uuid-last')
ORDER BY CreatedAt DESC, Id DESC;
```
* **Ưu điểm**: Tốc độ O(log N) cực nhanh bất kể độ sâu của trang. Phù hợp tuyệt đối cho **Infinite Scroll**.
<a name="chuong-6"></a>
## CHƯƠNG 6: XÁC THỰC, PHÂN QUYỀN & BẢO MẬT API (SECURITY & IDENTITY)

### 6.1 Authentication Vs Authorization Vs Identification

1. **Identification (Định danh)**: "Bạn khai báo bạn là ai?" (Gửi Username/Email).
2. **Authentication (Xác thực)**: "Bằng chứng nào chứng minh bạn là chính chủ?" (Mật khẩu, Passkey, OTP, Chữ ký JWT).
3. **Authorization (Phân quyền)**: "Bạn đã xác thực rồi, nhưng bạn CÓ QUYỀN làm hành động này không?" (Role, Policy, Ownership check).

---

### 6.2 Kiến Trúc JWT (JSON Web Token) & Refresh Token Rotation

#### 1. Cấu Trúc JWT (RFC 7519)
JWT bao gồm 3 phần được Base64Url-encode và ngăn cách bởi dấu chấm (`.`): `Header.Payload.Signature`
* **Header**: Khai báo Algorithm (`HS256`, `RS256`) và Type (`JWT`).
* **Payload**: Chứa các Claims (`sub`, `email`, `role`, `exp`, `iss`, `aud`, `jti`). **Lưu ý**: Payload KHÔNG mã hóa bí mật, bất kỳ ai cũng decode xem được, tuyệt đối không lưu mật khẩu/sensitive data vào Claims.
* **Signature**: Chữ ký số tạo từ `HMACSHA256(Base64(Header) + "." + Base64(Payload), SecretKey)`. Giúp Server xác minh dữ liệu không bị can thiệp (Tamper-proof).

#### 2. Mô Hình Refresh Token Rotation Bảo Vệ Phiên Đăng Nhập
* **Access Token**: Ngắn hạn (short-lived, 15 phút), truyền qua header `Authorization: Bearer <token>`.
* **Refresh Token**: Dài hạn (long-lived, 7 ngày), lưu an toàn trong Database và Client HTTP-Only Cookie.
* **Quy trình Rotation**:
  1. Khi Access Token hết hạn, Client gửi Refresh Token lên endpoint `/api/v1/auth/refresh`.
  2. Server vô hiệu hóa (`IsUsed = true`) Refresh Token cũ, phát hành cặp Access Token MỚI + Refresh Token MỚI.
  3. **Cơ chế phát hiện Token Reuse**: Nếu Server nhận được một Refresh Token ĐÃ ĐƯỢC SỬ DỤNG trước đó -> Server nhận diện nguy cơ bất thường -> Server lập tức **THU HỒI TOÀN BỘ REFRESH TOKENS** thuộc về User đó, bắt buộc đăng nhập lại trên mọi thiết bị.

```csharp
public class RefreshToken
{
    public Guid Id { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public bool IsUsed { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    
    public bool IsValid() => !IsUsed && !IsRevoked && ExpiresAt > DateTime.UtcNow;
    public void MarkUsed() => IsUsed = true;
    public void Revoke() => IsRevoked = true;
}
```

---

### 6.3 Các Mô Hình Phân Quyền Trong ASP.NET Core

#### 1. Role-Based Access Control (RBAC)
Phân quyền theo Vai trò người dùng (`Admin`, `Author`, `Reader`).
```csharp
app.MapPost("/api/v1/categories", Handler).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
```

#### 2. Policy-Based Authorization
Phân quyền theo chính sách linh hoạt dựa trên tổ hợp Claims.
```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AuthorOrAdmin", policy => policy.RequireRole("Author", "Admin"))
    .AddPolicy("MustBeSeniorChef", policy => policy.RequireClaim("ChefLevel", "Senior", "Master"));
```

#### 3. Resource-Based Authorization (Phân Quyền Cấp Đối Tượng)
Bảo vệ tài nguyên cấp đối tượng (ví dụ: Tác giả chỉ được sửa/xóa công thức do chính mình tạo ra).

```csharp
public class RecipeAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, Recipe>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        OperationAuthorizationRequirement requirement, 
        Recipe resource)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (requirement.Name == "Update" || requirement.Name == "Delete")
        {
            if (resource.AuthorId == userId)
                context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```
<a name="chuong-7"></a>
## CHƯƠNG 7: CHIẾN LƯỢC CACHING TRONG ỨNG DỤNG WEB & REDIS DISTRIBUTED CACHE

### 7.1 Đánh Đổi Cốt Lõi: Performance Vs Consistency

Caching là kỹ thuật đánh đổi giữa **Tốc độ phản hồi (Latency/Throughput)** và **Tính nhất quán dữ liệu (Consistency/Freshness)**.
* **Hit Ratio**: Tỷ lệ `Hits / (Hits + Misses)`. Mục tiêu Hit Ratio > 80% - 90%.
* **TTL (Time To Live)**: Thời gian sống của dữ liệu trong Cache trước khi bị Evict.

---

### 7.2 Các Tầng Cache Trong Hệ Thống Web

1. **Browser Cache**: Luôn gần user nhất, kiểm soát qua HTTP Headers `Cache-Control`, `ETag`.
2. **CDN / Edge Cache (Cloudflare, Akamai)**: Cache tĩnh/bán tĩnh ở PoP gần vị trí địa lý người dùng.
3. **Reverse Proxy Cache (Nginx, YARP)**: Cache mức Gateway.
4. **Application In-Memory Cache (`IMemoryCache`)**: Lưu trực tiếp RAM của App Process. Cực nhanh (nanoseconds), nhưng không đồng bộ khi Scale nhiều instance.
5. **Distributed Cache (Redis)**: Kho lưu trữ cache tập trung bằng RAM ngoài process, chia sẻ chung cho toàn bộ App instances.

---

### 7.3 Caching Patterns Với Redis

#### 1. Cache-Aside Pattern (Lazy Loading - Phổ Biến Nhất)
1. App nhận request đọc dữ liệu -> Kiểm tra Redis.
2. Nếu **Cache HIT**: Trả về ngay cho Client.
3. Nếu **Cache MISS**: Query xuống DB -> Lưu kết quả vào Redis (kèm TTL) -> Trả về Client.

```csharp
public async Task<RecipeDto?> GetRecipeByIdAsync(Guid id, CancellationToken ct)
{
    string cacheKey = $"recipes:{id}";
    var cachedData = await _distributedCache.GetStringAsync(cacheKey, ct);

    if (!string.IsNullOrEmpty(cachedData))
    {
        return JsonSerializer.Deserialize<RecipeDto>(cachedData);
    }

    var recipe = await _context.Recipes
        .AsNoTracking()
        .ProjectToType<RecipeDto>()
        .FirstOrDefaultAsync(r => r.Id == id, ct);

    if (recipe != null)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };
        await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(recipe), options, ct);
    }

    return recipe;
}
```

#### 2. Các Pattern Khác:
* **Write-Through**: Ghi đồng thời vào Cache và Database.
* **Write-Behind (Write-Back)**: Ghi vào Cache trước, trả về response ngay, sau đó có Worker đồng bộ bất đồng bộ xuống DB.
* **Refresh-Ahead**: Tự động refresh dữ liệu hot trước khi TTL hết hạn.

---

### 7.4 Sự Cố Cache Thực Tế & Cách Phòng Tránh

1. **Cache Stampede (Thundering Herd)**:
   * *Hiện tượng*: Một Key siêu "hot" bị hết hạn TTL đúng lúc có hàng ngàn request đồng thời dội vào -> Cả nghìn request cùng Cache Miss và dội xuống DB gây quá tải DB.
   * *Khắc phục*: Dùng Distributed Mutex Lock (Redlock) hoặc `GetOrCreate` có Semaphore Lock tại App level.
2. **Cache Penetration**:
   * *Hiện tượng*: Request liên tục truy vấn các Key KHÔNG TỒN TẠI trong hệ thống -> Bị bypass qua Cache và luôn dội thẳng xuống DB.
   * *Khắc phục*: Cache cả kết quả Null (Negative Caching với TTL ngắn 1-2 phút) hoặc dùng Bloom Filter.
<a name="chuong-8"></a>
## CHƯƠNG 8: LOGGING TẤP TRUNG, STRUCTURED LOGGING & TRACEABILITY

### 8.1 Structured Logging Vs String Interpolation

* **String Interpolation (❌ SAI)**:
  `logger.LogInformation($"User {userId} updated recipe {recipeId}");`
  -> Log bị biến thành chuỗi thô, các công cụ phân tích (Seq/Elasticsearch) không thể parse hay filter theo trường `userId` hay `recipeId`.
* **Structured Logging (✅ ĐÚNG)**:
  `logger.LogInformation("User {UserId} updated recipe {RecipeId}", userId, recipeId);`
  -> Message Template giữ nguyên. `UserId` và `RecipeId` được lưu dưới dạng thuộc tính JSON có cấu trúc, cho phép query `UserId = '123' AND RecipeId = '456'`.

---

### 8.2 Tích Hợp Serilog Trong ASP.NET Core

```json
// appsettings.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/culinaryblog-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

---

### 8.3 Correlation ID & Distributed Tracing Pipeline

Middleware tạo hoặc đọc `X-Correlation-ID` truyền qua HTTP Header để gắn vết toàn bộ hành trình của request xuyên suốt qua Next.js Server -> API Gateway -> Backend Microservices -> Database:

```csharp
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : Guid.NewGuid().ToString();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```
<a name="chuong-9"></a>
## CHƯƠNG 9: KIẾN TRÚC FRONTEND HIỆN ĐẠI VỚI NEXT.JS APP ROUTER & REACT SERVER COMPONENTS

### 9.1 Cấu Trúc App Router & Các File Đặc Biệt (Next.js 15/16)

App Router hoạt động dựa trên cấu trúc thư mục `/app`.

#### Cấu trúc Thư mục Chuẩn:
* `app/layout.tsx`: Root layout bọc toàn bộ ứng dụng (chứa html, body, Providers).
* `app/page.tsx`: UI của trang chủ `/`.
* `app/(public)/recipes/page.tsx`: Route Group `(public)` không ảnh hưởng đến URL path.
* `app/(public)/recipes/[slug]/page.tsx`: Dynamic Route Segment.
* `loading.tsx`: Streaming UI Skeleton tự động bọc trong `<Suspense>`.
* `error.tsx`: Catch runtime errors cấp subtree (`'use client'`).
* `proxy.ts` (Next.js 16) / `middleware.ts`: Chặn và xử lý request trước khi vào route handler.

---

### 9.2 Rendering Strategies Trong Next.js

1. **CSR (Client-Side Rendering)**: `'use client'` + TanStack Query (`useQuery`). Phù hợp trang Dashboard cá nhân hóa, trang tìm kiếm tương tác cao.
2. **SSR (Server-Side Rendering)**: Render HTML trên Server theo từng HTTP Request. Đảm bảo dữ liệu realtime.
3. **SSG (Static Site Generation)**: Build sẵn HTML từ lúc compile (`next build`). Cực nhanh, hỗ trợ CDN caching.
4. **ISR (Incremental Static Regeneration)**: Tự động re-render trang tĩnh ở background sau khoảng thời gian `revalidate`.
5. **Cache Components (`'use cache'`) & PPR (Partial Prerendering)** (Next.js 16):
   * Kết hợp Static Shell (prerendered lúc build) và Dynamic Streams (stream từ server qua `<Suspense>` tại runtime).

```typescript
// Next.js 16 Cache Components Example
import { cacheLife, cacheTag } from 'next/cache';

export async function getRecipeBySlug(slug: string) {
  'use cache';
  cacheLife('hours'); // Profile cache 1 giờ
  cacheTag(`recipe:${slug}`);

  const res = await fetch(`${process.env.API_URL}/api/v1/recipes/${slug}`);
  return res.json();
}
```

---

### 9.3 React Server Components (RSC) Vs Client Components

| Tiêu Chí | React Server Components (RSC - Mặc định) | Client Components (`'use client'`) |
| :--- | :--- | :--- |
| **Môi trường chạy** | Chỉ chạy trên Node.js Server. Zero JS bundle gửi xuống Client. | Hydrate và chạy trên cả Server lẫn Browser Client. |
| **Data Fetching** | Gọi async/await trực tiếp DB, Internal Services hoặc API. | Dùng `useEffect` hoặc TanStack Query (`useQuery`). |
| **Khả năng sử dụng**| KHÔNG được dùng `useState`, `useEffect`, Event Listeners (`onClick`). | Dùng đầy đủ React Hooks, Browser APIs (`window`, `localStorage`). |
| **Bảo mật** | An toàn cho API Keys, Database Secrets. | Mọi code trong Client Component đều được chuyển xuống Browser. |

---

### 9.4 Quản Lý Server State Phía Client Với TanStack Query (React Query) V5

```typescript
// Custom Hook lấy dữ liệu và Caching phía Client
import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api/axios';

export const recipeKeys = {
  all: ['recipes'] as const,
  detail: (slug: string) => [...recipeKeys.all, 'detail', slug] as const,
};

export function useRecipeDetail(slug: string) {
  return useQuery({
    queryKey: recipeKeys.detail(slug),
    queryFn: async () => {
      const { data } = await apiClient.get(`/api/v1/recipes/${slug}`);
      return data;
    },
    staleTime: 5 * 60 * 1000, // 5 phút không refetch
  });
}
```
<a name="chuong-10"></a>
## CHƯƠNG 10: QUY TRÌNH TỔNG HỢP DỰ ÁN THỰC TẾ (CULINARY BLOG CASE STUDY)

### 10.1 Luồng Xử Lý Request End-To-End Chuẩn Enterprise

```text
[Browser Client]
       │  (1) HTTP Request (GET /api/v1/recipes/pho-bo) + JWT Bearer
       ▼
[Next.js App Router Server / CDN Edge]
       │  (2) Static Shell Prerendered + RSC Stream (<Suspense>)
       ▼
[ASP.NET 10 Minimal API Pipeline]
       │  (3) Middleware: CorrelationId -> ExceptionHandler -> RateLimiter -> Authentication -> Authorization
       ▼
[Application Layer (MediatR CQRS)]
       │  (4) LoggingBehavior -> ValidationBehavior -> GetRecipeBySlugQueryHandler
       ▼
[Infrastructure Layer (EF Core & Redis Cache)]
       │  (5) Check Redis Cache (recipes:pho-bo)
       │      ├─ Cache HIT  ─> Trả về DTO ngay lập tức (1ms)
       │      └─ Cache MISS ─> Exec SQL qua EF Core (AsNoTracking + ProjectToType + FTS) 
       │                       ─> Set Redis Cache ─> Trả về DTO
       ▼
[JSON Response (RFC 7807 / Standard DTO)]
```

---

### 10.2 Summary Key Takeaways Dành Cho AI Training & System Prompt Reference

1. **Architecture**: Tuân thủ Clean Architecture 4 tầng chuẩn (`Domain`, `Application`, `Infrastructure`, `API`). Sử dụng CQRS kết hợp MediatR và Vertical Slice cho tính năng.
2. **API Design**: Dùng Minimal API .NET 10, TypedResults, RESTful Resource Naming (Danh từ số nhiều), ProblemDetails (RFC 7807), OpenAPI 3.1 & Scalar.
3. **Database & EF Core**: Dùng Data Mapper Pattern. Tối ưu N+1 qua Eager Loading / Projection (`ProjectToType`). Dùng `.AsNoTracking()` cho Read-only queries, `.AsSplitQuery()` cho đa Collection Includes. Tạo Non-Clustered Covering Index và PostgreSQL FTS (`tsvector` + GIN Index).
4. **Security**: Sử dụng PBKDF2 cho Password Hashing, JWT (HS256/RS256) + Refresh Token Rotation. Áp dụng Resource-Based Authorization bảo vệ tài nguyên đối tượng.
5. **Caching**: Áp dụng Cache-Aside Pattern với Redis Distributed Cache. Kết hợp Tag-based Invalidation trên Output Cache. Dùng Correlation ID theo dõi vết Request.
6. **Frontend**: Next.js App Router (`app/`), tận dụng React Server Components cho SEO & 0-bundle size. Dùng TanStack Query v5 quản lý Client-side Server State và Auth.js v5 quản lý Session an toàn qua HTTP-Only Cookie.
