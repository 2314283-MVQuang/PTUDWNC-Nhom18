# 📝 Phân công Công việc Chi tiết theo Tuần — Culinary Blog

> Tài liệu này triển khai chi tiết hơn Mục 7 của `README.md`, chia theo **Tuần** (giống định dạng bảng phân công gốc). Với mỗi tuần, mỗi thành viên biết rõ: **làm chức năng nào** (mã FR), **hướng đi / cách làm** ra sao, và **kết quả cụ thể** cần đạt được khi làm xong (để tự kiểm tra trước khi báo cáo nhóm).

---

## 📅 Tuần 1 — Đọc & Phân tích SRS (cả nhóm) ✅ Xong

| Thành viên | Chức năng / Nội dung đọc | Hướng đi | Kết quả khi hoàn thành |
| :--- | :--- | :--- | :--- |
| Mai Văn Quang | Toàn bộ SRS v1.1.0 (71 trang), đặc biệt Chương 6 (Kiến trúc) | Đọc tuần tự từ Chương 1 → 8; ghi chú riêng phần Auth, Category, Recipe, Search (module mình sẽ code) | Nắm được toàn cảnh hệ thống; lập được danh sách câu hỏi/điểm chưa rõ để hỏi nhóm ở Buổi 2 |
| Chung Thiện Ý | SRS phần Auth (Chương 3.1), Category (3.2), Recipe (3.3) | Đọc kỹ đặc tả 3 module mình có FR; đối chiếu với Mục 3 "Các mâu thuẫn đã giải quyết" trong `project_readme.md` | Hiểu rõ FR-AUTH-003/004, FR-CAT-001, FR-RCP-003/005/009 mình sẽ làm |
| Nguyễn Ngọc Bảo Thịnh | SRS phần Category, Recipe, Search, NFR-PERF | Đọc đặc tả + bảng NFR-PERF-003 (cache TTL) trong Mục 5 `project_readme.md` | Hiểu rõ FR mình phụ trách và ngưỡng hiệu năng cần đạt (cache hit ≥ 80%) |
| Hồ Quốc Tiến | SRS phần Auth, File, Job, Observability, NFR-REL/NFR-SCALE | Đọc kỹ NFR-REL-003 (soft delete) và NFR-SCALE-001 (Redis thay IMemoryCache) — đây là 2 NFR ảnh hưởng nhiều FR của mình | Hiểu rõ vì sao Recipe/Category dùng soft delete, vì sao không dùng IMemoryCache |
| Mai Văn Quang | Chủ trì họp; khởi tạo repo & hạ tầng | Họp cả nhóm chốt: kiến trúc Clean Architecture + CQRS/MediatR, cách chia FR (round-robin trong từng module); tạo repo Git, khung `docker-compose.yml` (Postgres, Redis, MinIO, API, Frontend), khung Next.js 15 | Repo tồn tại trên GitHub, `docker compose up` chạy được các service rỗng, mọi người `git clone` và build được |
| Chung Thiện Ý | Tham gia họp; xác nhận FR mình nhận | Đối chiếu FR đã nhận (AUTH-003/004, CAT-001, RCP-003/005/009, SRCH-002, JOB-001, OBS-001) với bảng phân công | Biết chắc chắn tuần nào mình làm FR nào, không nhầm lẫn với người khác |
| Nguyễn Ngọc Bảo Thịnh | Tham gia họp; xác nhận FR mình nhận | Đối chiếu FR đã nhận (AUTH-005/006, CAT-003, RCP-002/007/008, SRCH-003, FILE-002, OBS-002) | Biết chắc chắn tuần nào mình làm FR nào |
| Hồ Quốc Tiến | Tham gia họp; xác nhận FR mình nhận | Đối chiếu FR đã nhận (AUTH-007, CAT-004, RCP-004/010, SRCH-004, FILE-001, JOB-002/003, OBS-003) | Biết chắc chắn tuần nào mình làm FR nào |

## 📅 Tuần 2 — Module Xác thực & Người dùng (FR-AUTH-001, FR-AUTH-002) ✅ Xong

| Thành viên | Chức năng | Hướng đi | Kết quả khi hoàn thành | Trạng thái |
| :--- | :--- | :--- | :--- | :-: |
| Mai Văn Quang | Tạo cấu trúc dự án backend theo Clean Architecture | Tạo 4 project: **Domain** (entities/enums, không phụ thuộc layer khác), **Application** (CQRS commands/queries, interfaces, DTOs), **Infrastructure** (EF Core, Identity, Redis, MinIO), **Presentation/API** (Minimal API, DI) — đúng dependency rule NFR-MAINT-004 | Solution build thành công, 4 project tách biệt rõ, project Domain không reference bất kỳ project nào khác | ✅ Đã hoàn thành |
| Mai Văn Quang | Cài đặt các gói thư viện cần thiết | Cài `MediatR`, `FluentValidation`, `EF Core` + `Npgsql`, `Identity.EntityFrameworkCore`, `Serilog` + `Serilog.Sinks.Seq`, `Hangfire` (Core/AspNetCore/PostgreSql), `AWSSDK.S3`, `Bogus`, `Scalar`/Swashbuckle | `dotnet restore`/`dotnet build` chạy sạch, không thiếu package khi các module khác bắt đầu code | ✅ Đã hoàn thành |
| Mai Văn Quang | Cài đặt các lớp entities, configuration, DbContext | Viết entity chính (`ApplicationUser`, `Category`, `Recipe`, `RecipeStep`, `RecipeIngredient`, `RefreshToken`...); mỗi entity có `IEntityTypeConfiguration<T>` riêng; `AppDbContext` kế thừa `IdentityDbContext`, đăng ký configuration qua `ApplyConfigurationsFromAssembly` | Toàn bộ entity + configuration compile được, `AppDbContext` khởi tạo thành công khi chạy app | ✅ Đã hoàn thành |
| Mai Văn Quang | Tạo migration; cài đặt lớp sinh dữ liệu ngẫu nhiên | `dotnet ef migrations add InitialCreate` → `dotnet ef database update`; viết `DataSeeder`/`FakeDataGenerator` dùng `Bogus` cho Category/Recipe/Ingredient/Step | Database được tạo với đầy đủ bảng đúng schema; chạy seeder không lỗi | ✅ Đã hoàn thành |
| Mai Văn Quang | Đảm bảo CSDL có dữ liệu ngẫu nhiên đủ yêu cầu | Chạy seeder rồi kiểm tra bằng `psql`/pgAdmin: đếm số dòng Categories/Recipes, dùng `GROUP BY`/`HAVING` để kiểm tra số ingredient/step từng recipe | CSDL có **≥ 20 categories**, **≥ 100 recipes**, mỗi recipe có **≥ 10 nguyên liệu** và **≥ 5 bước chế biến** — đủ điều kiện tối thiểu Lab 2 | ✅ Đã hoàn thành |
| Mai Văn Quang | **FR-AUTH-001** Đăng ký tài khoản | Viết `RegisterCommand` (MediatR) + `RegisterCommandValidator` (FluentValidation) kiểm tra email trùng, độ mạnh mật khẩu; dùng `UserManager.CreateAsync` (Identity tự hash PBKDF2) | `POST /auth/register` tạo được user mới, trả 201 kèm thông tin user (không có password); gọi lại với email đã tồn tại trả lỗi rõ ràng | ✅ Đã hoàn thành |
| Mai Văn Quang | **FR-AUTH-002** Đăng nhập email/mật khẩu | `LoginCommand` dùng `SignInManager.CheckPasswordSignInAsync`; phát JWT HS256 (claim userId/email/roles/jti, TTL 15p) + Refresh Token (random 128-bit, hash SHA-256 lưu DB, TTL 7 ngày) | `POST /auth/login` trả `{accessToken, refreshToken}` khi đúng thông tin; trả 401 khi sai email/mật khẩu | ✅ Đã hoàn thành |
| Mai Văn Quang | Kiểm thử luồng Auth end-to-end | Test thủ công (Postman/Scalar): đăng ký → đăng nhập đúng mật khẩu → đăng nhập sai mật khẩu → đăng ký lại email đã tồn tại | FR-AUTH-001/002 hoạt động thông suốt, sẵn sàng làm nền JWT cho các phần còn lại | ✅ Đã hoàn thành |

## 📅 Tuần 3 — Module Quản lý Danh mục (FR-CAT)

| Thành viên | Chức năng | Hướng đi | Kết quả khi hoàn thành |
| :--- | :--- | :--- | :--- |
| Chung Thiện Ý | **FR-CAT-001** Xem danh sách danh mục | `GetCategoriesQuery`; áp Output Cache (Redis) TTL 30 phút theo NFR-PERF-003 | `GET /categories` trả danh sách; gọi lần 2 trong vòng 30 phút phản hồi nhanh hơn rõ rệt (cache hit) |
| Mai Văn Quang | **FR-CAT-002** Xem chi tiết danh mục (kèm recipe) | `GetCategoryByIdQuery` join sang các Recipe đã publish thuộc danh mục, có phân trang | `GET /categories/{id}` trả thông tin danh mục + danh sách recipe (phân trang đúng, chỉ recipe đã publish) |
| Nguyễn Ngọc Bảo Thịnh | **FR-CAT-003** Tạo danh mục mới | `CreateCategoryCommand` [Admin]; sinh slug tự động từ tên, kiểm tra trùng tên trước khi lưu | `POST /categories` (chỉ Admin) tạo được danh mục mới với slug hợp lệ; user thường gọi bị trả 403 |
| Hồ Quốc Tiến | **FR-CAT-004** Cập nhật danh mục | `UpdateCategoryCommand` [Admin]; nếu đổi tên khiến slug trùng danh mục khác thì **tự thêm hậu tố** (vd `-2`) | `PUT /categories/{id}` cập nhật thành công; đặt tên trùng danh mục khác vẫn lưu được (slug tự đổi), không báo lỗi |
| Mai Văn Quang | **FR-CAT-005** Xóa danh mục | `DeleteCategoryCommand` [Admin] chỉ set `IsDeleted = true` (soft delete), thêm Global Query Filter ẩn khỏi mọi query | `DELETE /categories/{id}` xong thì danh mục biến mất khỏi `GET /categories`, nhưng dữ liệu vẫn còn trong DB (có thể khôi phục bằng script) |
| Cả nhóm | Kiểm thử module Category | Test 5 FR-CAT end-to-end, đối chiếu với FR-RCP-002 (chi tiết công thức có hiển thị category không) | Module Category hoàn chỉnh, sẵn sàng để Recipe tham chiếu `CategoryId` ở Tuần 4 |

## 📅 Tuần 4 — Module Công thức (phần lõi) & Job Thumbnail

| Thành viên | Chức năng | Hướng đi | Kết quả khi hoàn thành |
| :--- | :--- | :--- | :--- |
| Mai Văn Quang | **FR-RCP-001** Danh sách công thức (phân trang/lọc/sắp xếp) | `GetRecipesQuery` hỗ trợ `page`/`pageSize`, lọc theo `categoryId`/`difficulty`, sắp xếp theo field; Output Cache TTL 5 phút | `GET /recipes` trả danh sách đúng theo tham số lọc/sắp xếp/phân trang; kết quả được cache 5 phút |
| Nguyễn Ngọc Bảo Thịnh | **FR-RCP-002** Chi tiết công thức | `GetRecipeByIdQuery` trả đầy đủ `steps`, `ingredients`, ảnh, thông tin category/author; tăng `viewCount` | `GET /recipes/{id}` trả đủ dữ liệu để frontend render trang chi tiết hoàn chỉnh |
| Chung Thiện Ý | **FR-RCP-003** Tạo công thức mới | `CreateRecipeCommand` [Author/Admin]; entity `Recipe` mới có status `Draft`; slug tự sinh, **tự thêm hậu tố khi trùng** (không trả lỗi 409 slug trùng) | `POST /recipes` tạo công thức ở trạng thái Draft; đặt tên trùng công thức khác vẫn tạo được (slug tự đổi) |
| Hồ Quốc Tiến | **FR-RCP-004** Cập nhật thông tin cơ bản | `UpdateRecipeCommand` [Author-Owner/Admin] dùng `RowVersion` (concurrency token); bắt `DbUpdateConcurrencyException` → trả **409 Conflict** | Sửa công thức bình thường thành công; nếu 2 người cùng sửa 1 công thức, người lưu sau nhận lỗi 409 (không bị mất dữ liệu ngầm) |
| Chung Thiện Ý | **FR-RCP-005** Publish công thức | `PublishRecipeCommand` chỉ cho publish khi **≥ 1 RecipeStep VÀ ≥ 1 RecipeIngredient** | `PATCH /recipes/{id}/publish` thành công khi công thức đủ điều kiện; thiếu bước hoặc nguyên liệu → trả lỗi `RECIPE_PUBLISH_INCOMPLETE` |
| Hồ Quốc Tiến | **FR-JOB-002** Sinh Thumbnail | Hangfire job tự trigger ngay sau khi upload ảnh công thức, resize ra nhiều kích thước (thumbnail/medium/large), lưu lại MinIO | Sau khi upload 1 ảnh, hệ thống tự sinh thêm các bản thumbnail mà không cần gọi API riêng |

## 📅 Tuần 5 — Module Công thức (ảnh/nguyên liệu/xóa), Job Sitemap & Observability

| Thành viên | Chức năng | Hướng đi | Kết quả khi hoàn thành |
| :--- | :--- | :--- | :--- |
| Mai Văn Quang | **FR-RCP-006** Hủy publish / Lưu trữ (Archive) | `ArchiveRecipeCommand` chuyển status sang `Archived`, không hiển thị công khai nhưng dữ liệu vẫn giữ nguyên | `PATCH /recipes/{id}/archive` thành công; công thức biến mất khỏi trang public nhưng tác giả vẫn xem được trong "công thức của tôi" |
| Nguyễn Ngọc Bảo Thịnh | **FR-RCP-007** Xóa công thức | `DeleteRecipeCommand` chỉ set `IsDeleted = true` (**soft delete**, đã sửa theo NFR-REL-003 — không hard delete như bản gốc) | `DELETE /recipes/{id}` xong thì công thức biến mất khỏi danh sách/tìm kiếm, nhưng vẫn còn trong DB (chờ purge sau 30 ngày) |
| Nguyễn Ngọc Bảo Thịnh | **FR-RCP-008** Quản lý ảnh công thức | `AddRecipeImageCommand`/`SetPrimaryImageCommand`/`DeleteRecipeImageCommand`, upload qua presigned URL MinIO | Thêm/xóa/đặt ảnh đại diện cho công thức hoạt động đúng; ảnh hiển thị được ở trang chi tiết |
| Chung Thiện Ý | **FR-RCP-009** Quản lý nguyên liệu | CRUD `RecipeIngredient` (thêm/sửa/xóa, có thứ tự `sortOrder`) | Thêm/sửa/xóa nguyên liệu cho công thức, hiển thị đúng thứ tự đã sắp xếp |
| Hồ Quốc Tiến | **FR-RCP-010** Quản lý các bước thực hiện | CRUD `RecipeStep` (thêm/sửa/xóa, `sortOrder`, có thể đính ảnh minh họa từng bước) | Thêm/sửa/xóa bước thực hiện, hiển thị đúng thứ tự, mỗi bước có thể có ảnh riêng |
| Hồ Quốc Tiến | **FR-JOB-003** Sinh Sitemap | Hangfire Recurring Job chạy 02:00 AM hàng ngày, sinh `sitemap.xml` cho recipe/category đã publish, gọi ping Google Search Console | Truy cập `/sitemap.xml` thấy đủ URL các trang đã publish, cập nhật tự động mỗi ngày không cần thao tác tay |
| Chung Thiện Ý | **FR-OBS-001** Health Check Endpoints | Cấu hình `AspNetCore.HealthChecks.*` cho 3 endpoint: `/health` (tổng hợp), `/health/live`, `/health/ready` | Tắt thử Postgres/Redis → `/health/ready` trả 503; bật lại → trả 200. `/health/live` luôn trả 200 khi app còn chạy |
| Nguyễn Ngọc Bảo Thịnh | **FR-OBS-002** Structured Logging (Serilog) | Serilog + `CorrelationIdMiddleware`; `LoggingBehavior` trong MediatR log mọi Command/Query; sink Console (JSON) + File + Seq | Mở Seq (dev) thấy log có `CorrelationId`, method/path/status, thời gian xử lý cho mọi request gọi vào hệ thống |

## 📅 Tuần 6 — Module Tìm kiếm & Phân trang, Distributed Tracing

| Thành viên | Chức năng | Hướng đi | Kết quả khi hoàn thành |
| :--- | :--- | :--- | :--- |
| Mai Văn Quang | **FR-SRCH-001** Tìm kiếm toàn văn bản | PostgreSQL `tsvector`/`tsquery` + extension `unaccent`; xếp hạng theo `ts_rank` | `GET /recipes/search?q=...` tìm được công thức dù gõ **có dấu hoặc không dấu** (vd "pho bo" ra "Phở bò"), kết quả liên quan nhất lên trước |
| Chung Thiện Ý | **FR-SRCH-002** Lọc công thức | Query filter theo `categoryId`, `difficulty`, khoảng thời gian chuẩn bị/nấu qua query string | Truyền tham số lọc trả đúng tập kết quả thỏa điều kiện, kết hợp được nhiều điều kiện lọc cùng lúc |
| Nguyễn Ngọc Bảo Thịnh | **FR-SRCH-003** Sắp xếp kết quả (sort=-field) | Parse tham số `sort` (vd `-createdAt`, `title`), map động sang `OrderBy`/`OrderByDescending` | `?sort=-createdAt` trả công thức mới nhất trước; đổi field khác vẫn sắp xếp đúng |
| Hồ Quốc Tiến | **FR-SRCH-004** Phân trang | Chuẩn hóa `page`/`pageSize`, trả metadata `totalCount`/`totalPages`; Output Cache TTL 1 phút cho kết quả search | Kết quả tìm kiếm/lọc/sắp xếp đều phân trang đúng số liệu; gọi lại trong 1 phút phản hồi từ cache |
| Hồ Quốc Tiến | **FR-OBS-003** Distributed Tracing & Metrics | OpenTelemetry instrument HTTP request + EF Core traces, export OTLP đến Seq (dev)/Grafana Tempo (prod); custom metric đếm recipe created/published | Mở Seq/Tempo thấy được đường trace đầy đủ của 1 request đi qua API → EF Core → DB; xem được số liệu recipe created/published theo thời gian |
| Cả nhóm | Kiểm thử module Search & Observability | Test tìm kiếm tiếng Việt không dấu, kết hợp lọc+sắp xếp+phân trang; kiểm tra log/trace/health check đồng bộ | Module Search hoạt động ổn định; có đủ log + trace + health check để debug khi triển khai thật |

## 📅 Tuần 7 — Module Quản lý Tệp tin & Job Welcome Email

| Thành viên | Chức năng | Hướng đi | Kết quả khi hoàn thành |
| :--- | :--- | :--- | :--- |
| Hồ Quốc Tiến | **FR-FILE-001** Upload file lên MinIO | AWSSDK.S3 (endpoint override cho MinIO); sinh presigned URL, client upload trực tiếp lên bucket; kiểm tra MIME qua magic bytes (không tin Content-Type header) | Client lấy được presigned URL và upload ảnh thành công thẳng lên MinIO (không qua backend proxy); upload file giả dạng ảnh (sai magic bytes) bị từ chối |
| Nguyễn Ngọc Bảo Thịnh | **FR-FILE-002** Xóa file khỏi MinIO | Xóa object theo key khi ảnh bị gỡ khỏi Recipe/Category, hoặc khi `PurgeDeletedRecipesJob` chạy | Gỡ ảnh khỏi công thức thì file cũng biến mất khỏi bucket MinIO (không để rác) |
| Chung Thiện Ý | **FR-JOB-001** Welcome Email (Hangfire) | Enqueue job ngay sau khi `RegisterCommand` thành công, gửi email qua MailKit (SMTP; dev dùng MailHog) | Đăng ký tài khoản mới xong, kiểm tra MailHog thấy email chào mừng được gửi tới đúng địa chỉ |
| Cả nhóm | Rà soát toàn bộ 34 FR trước khi tích hợp | Mỗi người tự kiểm tra lại các FR mình phụ trách theo cột "Kết quả khi hoàn thành" trong tài liệu này | Có danh sách FR nào còn thiếu/lỗi để xử lý ngay đầu Tuần 8, tránh dồn việc vào buổi tích hợp cuối |

## 📅 Tuần 8 — Tích hợp, Kiểm thử, Triển khai (cả nhóm)

| Thành viên | Chức năng | Hướng đi | Kết quả khi hoàn thành |
| :--- | :--- | :--- | :--- |
| Mai Văn Quang | Integration test toàn hệ thống | Viết/ chạy test end-to-end (WebApplicationFactory) cho các luồng chính: đăng ký→đăng nhập→tạo công thức→publish→tìm kiếm→xóa | Toàn bộ luồng chính pass test, không còn lỗi 500 bất ngờ giữa các module |
| Chung Thiện Ý | Test luồng Auth + Recipe | Test thủ công + tự động luồng Auth (đăng ký→đăng nhập→refresh) ghép với Recipe (tạo→sửa→publish→xóa) | Không phát sinh lỗi khi 2 module hoạt động cùng nhau (vd AuthorId, phân quyền Author-Owner) |
| Nguyễn Ngọc Bảo Thịnh | Test Category/Search/File | Test Category + Search + File Upload, đối chiếu với dữ liệu Recipe thật | 3 module hoạt động đúng khi có dữ liệu Recipe/Category thật từ các thành viên khác |
| Hồ Quốc Tiến | Test Job/Observability/File | Kiểm tra Hangfire dashboard, Seq log, health check, sitemap sinh đúng khi hệ thống có dữ liệu thật | Toàn bộ background job chạy đúng lịch, log/trace/health check phản ánh đúng tình trạng hệ thống |
| Mai Văn Quang | Triển khai & tổng hợp báo cáo | Viết `docker-compose.prod.yml`, deploy thử lên server/VM; tổng hợp toàn bộ nội dung vào báo cáo dự án (dựa trên `project_readme.md`) | Hệ thống chạy được bằng `docker compose up` trên môi trường production-like; có báo cáo hoàn chỉnh để nộp |
| Chung Thiện Ý | Chuẩn bị demo phần Auth + Recipe | Chuẩn bị dữ liệu mẫu, kịch bản demo đăng nhập → tạo/publish công thức | Demo chạy trơn tru, không lỗi giữa các bước |
| Nguyễn Ngọc Bảo Thịnh | Chuẩn bị demo phần Category/Search/File | Chuẩn bị dữ liệu mẫu, kịch bản demo tìm kiếm không dấu, upload ảnh | Demo chạy trơn tru, không lỗi giữa các bước |
| Hồ Quốc Tiến | Chuẩn bị demo phần Job/Observability | Chuẩn bị màn hình Hangfire dashboard, Seq, health check để trình chiếu khi giảng viên hỏi về vận hành hệ thống | Có thể trình chiếu trực tiếp cho giảng viên thấy log/trace/job đang chạy thật, không chỉ là code tĩnh |
