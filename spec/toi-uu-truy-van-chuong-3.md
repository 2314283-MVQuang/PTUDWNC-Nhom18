# Đối chiếu dự án với Chương 3 — Thiết kế dữ liệu và Tối ưu truy vấn

Tài liệu đối chiếu từng kỹ thuật trong giáo trình Chương 3 với code Culinary Blog. Cột **Trước** là trạng thái lúc rà soát. Cột **Sau** là trạng thái sau đợt tối ưu.

| Mục giáo trình | Kỹ thuật | Trước | Sau | Vị trí trong code |
|---|---|---|---|---|
| 3.2 | Repository + Unit of Work, `IQueryable Query()`, 1 lần `SaveChangesAsync` | ✅ Có | ✅ | `Domain/Interfaces/IRepository.cs`, `IUnitOfWork.cs`, `Persistence/Repositories/*` |
| 3.4.1 | `IEntityTypeConfiguration<T>` + `ApplyConfigurationsFromAssembly` | ✅ Có | ✅ | `Persistence/Configurations/*` |
| 3.4.1 | Owned Entity (`RecipeNutrition`), concurrency token `RowVersion`, cascade/restrict delete | ✅ Có | ✅ | `RecipeConfiguration.cs`, `db/init/02-schema.sql` |
| 3.5.1 | Tránh N+1 | ⚠️ `GET /users` gọi `GetRolesAsync` cho **từng** người (20 người = 20 query) | ✅ 1 câu JOIN cho cả trang | `IUserRoleReader`, `UserRoleReader.cs`, `GetUsersQueryHandler.cs` |
| 3.5.2 | `AsSplitQuery()` khi Include nhiều collection | ✅ Chi tiết công thức (Steps, Ingredients, Images) | ✅ | `RecipeRepository.GetBy*WithDetailsAsync` |
| 3.5.3 | `AsNoTracking()` + Projection (chỉ SELECT cột cần) | ⚠️ Mới có ở `GET /recipes` và bước/nguyên liệu/ảnh. Trang danh mục, kết quả tìm kiếm, bình luận và sitemap vẫn Include rồi nạp **cả entity**: hàng `AspNetUsers` (PasswordHash, SecurityStamp…) và mọi ảnh; sitemap còn bật change tracking cho toàn bộ công thức | ✅ Cả 4 chỗ chuyển sang projection + AsNoTracking | `GetCategoryBySlugQueryHandler`, `RecipeRepository.SearchRecipesAsync`, `GetRecipeCommentsQueryHandler`, `SitemapBuilder` |
| 3.6 | Truy vấn trang danh mục viết để dùng được index | ⚠️ `ORDER BY "PublishedAt" IS NULL` (đẩy bản nháp xuống cuối) không dùng được index | ✅ Khách xem: lọc `Status = Published` + `ORDER BY PublishedAt DESC` → dùng index ghép; người đăng nhập giữ thứ tự cũ | `GetCategoryBySlugQueryHandler` |
| 3.5.3 | Phân trang offset có thứ tự ổn định | ⚠️ Thiếu khoá phụ khi trùng `CreatedAt`/`Title` | ✅ Thêm `ThenBy(Id)` | `GetRecipesQueryHandler`, `GetCategoryBySlugQueryHandler` |
| 3.5.4 | Bật SQL logging ở Development | ❌ Serilog đặt EF Core ở mức Warning nên không thấy SQL | ✅ `Microsoft.EntityFrameworkCore.Database.Command = Information` (chỉ Development) | `appsettings.Development.json` |
| 3.6.1 | B-tree index theo truy vấn thật | ⚠️ Thiếu index cho sắp xếp `CreatedAt` (GET /recipes), cho trang danh mục (lọc CategoryId + Status, sắp PublishedAt), cho job purge, cho thứ tự nguyên liệu/ảnh, cho `/users` | ✅ 6 index mới (bảng dưới) | `db/init/02-schema.sql`, `Configurations/*`; database cũ được thêm lúc API khởi động |
| 3.6.2–3.6.4 | Full-Text Search: `tsvector` + GIN, `unaccent`, cấu hình không stemming, `ts_rank` | ✅ Có. Cấu hình `vietnamese` = simple + unaccent, trọng số A (tiêu đề) / B (mô tả), `websearch_to_tsquery`, ORDER BY whitelist | ✅ | `db/init/01-extensions.sql`, `02-schema.sql`, `RecipeRepository.SearchRecipesAsync` |
| 3.6 / Bài 2 | Đo bằng `EXPLAIN ANALYZE` | ❌ Chưa có số đo | ✅ Script đo trước/sau trên 20 000 công thức giả (tự ROLLBACK) | `db/explain-analyze.sql`, `explain-analyze.ps1` |
| 3.7.1 | Dữ liệu cố định | ✅ Role + Admin seed khi khởi động | ✅ | `RoleSeeder.cs` |
| 3.7.2 | Bogus seed cố định (tái lập được) | ⚠️ `new Faker()` ngẫu nhiên mỗi lần | ✅ `Randomizer(42)` | `DbSeeder.cs` |

## Index bổ sung

| Index | Phục vụ truy vấn |
|---|---|
| `IX_Recipes_CreatedAt` (`CreatedAt DESC`) | `GET /recipes` mặc định mới nhất trước: đọc 12 dòng đầu theo index thay vì sắp cả bảng |
| `IX_Recipes_CategoryId_Status_PublishedAt` | `GET /categories/{slug}`: lọc danh mục + Published, sắp theo ngày xuất bản; đếm số công thức của danh mục |
| `IX_Recipes_DeletedAt_Purge` (partial `WHERE IsDeleted`) | Job purge 03:00: chỉ chứa hàng đã xoá mềm nên rất nhỏ |
| `IX_RecipeIngredients_RecipeId_OrderIndex`, `IX_RecipeImages_RecipeId_OrderIndex` | Chi tiết công thức: `WHERE RecipeId = … ORDER BY OrderIndex` không cần sắp lại (thay index 1 cột cũ) |
| `IX_AspNetUsers_CreatedAt` (`CreatedAt DESC`) | Trang quản lý người dùng sắp mới nhất trước |
| ~~`IX_Recipes_PublishedAt`~~ (**đã bỏ**) | Đo cho thấy planner chọn index này cho trang danh mục rồi phải bỏ qua 3 092 dòng (bản nháp có `PublishedAt` NULL nằm đầu index DESC). Không truy vấn nào khác cần nó. Bỏ đi để index ghép phục vụ trang danh mục, và bớt 1 index thì ghi dữ liệu cũng rẻ hơn. |

## Cách đo

```powershell
powershell -ExecutionPolicy Bypass -File .\explain-analyze.ps1
```

Script sinh 20 000 công thức giả trong 1 transaction và chạy `EXPLAIN (ANALYZE, BUFFERS)` cho các truy vấn:

- **Q1–Q3:** đo trước và sau khi có index mới. Q1 là `GET /recipes`, Q2 là trang danh mục, Q3 là job purge.
- **Q4:** so sánh `LIKE '%pho bo%'` với Full-Text Search dùng GIN.
- **Q5:** chi tiết công thức theo slug.

Cuối script là `ROLLBACK`, nên dữ liệu thật không đổi. Kết quả đo ghi ở mục **Kết quả đo** bên dưới.

## Kết quả đo

Đo ngày 03/11/2026 trên PostgreSQL 16 (Docker, bản `docker-compose.prod.yml`). Dữ liệu: 20 000 công thức (khoảng 86% Published), 20 danh mục, 200 công thức đã xoá mềm quá 30 ngày. Mọi thứ nằm trong transaction và ROLLBACK ở cuối. Kết quả đầy đủ ở [`explain-analyze-ketqua-2026-11-03.txt`](explain-analyze-ketqua-2026-11-03.txt).

| Truy vấn | Trước — kế hoạch | Trước | Sau — kế hoạch | Sau | Nhanh hơn |
|---|---|---|---|---|---|
| Q1. `GET /recipes` trang 1 (mới nhất trước) | Seq Scan 20 004 dòng + Sort | 5,523 ms | Index Scan `IX_Recipes_CreatedAt`, đọc 15 dòng | **0,053 ms** | ~104× |
| Q2. `GET /categories/{slug}` (khách xem) | Index Scan `IX_Recipes_PublishedAt`, loại 3 092 dòng | 1,584 ms | Index Scan `IX_Recipes_CategoryId_Status_PublishedAt`, Index Cond CategoryId + Status, loại 0 dòng | **0,064 ms** | ~25× |
| Q3. Job purge (xoá mềm > 30 ngày) | Seq Scan, loại 19 804 dòng | 2,463 ms | Index Scan `IX_Recipes_DeletedAt_Purge` (partial) | **0,082 ms** | ~30× |
| Q4. Tìm 1 món cụ thể | `LIKE '%bun cha kieu 12345%'`: Seq Scan, loại 20 003 dòng | 12,068 ms | Full-Text Search: Bitmap Index Scan GIN `IX_Recipes_SearchVector` + `ts_rank` | **2,778 ms** | ~4× |
| Q5. `GET /recipes/{slug}` | Index Scan unique `Recipes_Slug_key` | — | (giữ nguyên) | 0,021 ms | đã tối ưu sẵn |

**Nhận xét:**

- **Q1 và Q3:** trước khi có index, PostgreSQL phải đọc toàn bộ bảng. Thời gian này tăng tuyến tính theo số công thức. Sau khi có index, chỉ đọc khoảng 12–200 dòng cần thiết, nên thời gian gần như không đổi khi dữ liệu tăng.
- **Q2:** đổi 2 thứ mới hết chậm.
  - **Câu truy vấn:** viết lại để dùng được index. `ORDER BY ... IS NULL` không dùng được index nên bỏ cho khách xem.
  - **Index:** bỏ index 1 cột khiến planner chọn sai.
  - Lần đo trước, khi mới thêm index ghép mà chưa bỏ index cũ, planner vẫn chọn index cũ và thời gian không đổi. Đây đúng là tinh thần "measure before optimize" của giáo trình.
- **Q4:** `LIKE` có `%` ở đầu luôn phải quét cả bảng. Full-Text Search dùng GIN chỉ đọc các dòng có đủ từ khoá, kể cả khi gõ không dấu.
