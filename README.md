# 🍲 Culinary Blog - Dự án Phát triển Ứng dụng Web Nâng cao

> ⚠️ **Cập nhật phạm vi (mới nhất):** nhóm đã **gỡ bỏ code của các module Category, Recipe, Search, File, Job** khỏi source để làm gọn đúng yêu cầu tối thiểu Lab 2, **chỉ giữ lại module FR-AUTH** (đã hoàn thành) cùng khung dự án Clean Architecture. Toàn bộ nội dung Mục 4–7 dưới đây (mâu thuẫn SRS, FR/NFR, phân công theo buổi) vẫn được giữ lại làm **tài liệu tham khảo/kế hoạch gốc** — riêng phần **Buổi 3–8** (Category/Recipe/Search/File/Job) hiện **chưa khớp với code thật**, sẽ được nhóm phân công lại và cập nhật SPEC sau.

## 📑 Mục lục
1. [Tổng quan Dự án & Nhóm thực hiện](#sec-1)
2. [Kiến trúc Hệ thống & Công nghệ](#sec-2)
3. [Môi trường & Công cụ Cần thiết để Chạy Dự án](#sec-3)
4. [Các Mâu thuẫn đã Giải quyết trong SRS](#sec-4)
5. [Yêu cầu Chức năng (Functional Requirements)](#sec-5)
6. [Yêu cầu Phi Chức năng (Non-Functional Requirements)](#sec-6)
7. [Kế hoạch & Phân công Công việc Chi tiết theo Buổi](#sec-7)
8. [Quy tắc làm việc & Triển khai](#sec-8)

> 🔗 Mục lục dùng anchor HTML tường minh (`<a id="sec-N">`) đặt ngay trước mỗi heading, không phụ thuộc cách GitHub tự sinh id từ emoji/tiếng Việt — đảm bảo bấm vào luôn nhảy đúng chỗ.

> **Ghi chú hiệu chỉnh so với bản phân công gốc:** đã sửa **FR-RCP-007** từ "hard delete" thành **soft delete** (đúng theo NFR-REL-003 của SRS: *"Recipe được đánh dấu IsDeleted thay vì xóa vật lý"* — dữ liệu chỉ bị xóa cứng sau 30 ngày qua job định kỳ), và bổ sung **FR-OBS-003 (Distributed Tracing & Metrics)** — mục 3.7 của SRS quy định module FR-OBS có **3 FR** (Health Check, Structured Logging, Distributed Tracing & Metrics) chứ không phải 2 FR như bản gốc.

---

<a id="sec-1"></a>
## 👥 1. Tổng quan Dự án & Nhóm thực hiện

**Culinary Blog** là nền tảng web cho phép người dùng chia sẻ, khám phá và lưu trữ các công thức nấu ăn. Hệ thống được phân loại khoa học theo danh mục, hỗ trợ tìm kiếm toàn văn bản tiếng Việt và tích hợp các tính năng tối ưu trải nghiệm đọc - lưu trữ nội dung.

Nhóm phân công theo mô hình **luân phiên trong từng module** (không phải 1 người ôm trọn 1 module): mỗi module chức năng (FR-AUTH, FR-CAT, FR-RCP...) được chia đều cho cả 4 thành viên, mỗi người đảm nhận một vài FR cụ thể trong module đó — đảm bảo ai cũng va chạm đủ backend, database lẫn frontend ở nhiều module khác nhau. Chi tiết endpoint từng FR xem ở [Mục 5](#sec-5); phân công ai làm FR nào theo từng buổi xem ở [Mục 7](#sec-7).

| STT | Họ và tên | Mã sinh viên / Email | Vai trò chính |
| :-: | :--- | :--- | :--- |
| 1 | **Mai Văn Quang** 👨‍💻 | `2314283@dlu.edu.vn` | Tham gia xuyên suốt các module Auth, Category, Recipe, Search; phụ trách đăng ký/đăng nhập, chi tiết & xóa danh mục, danh sách & lưu trữ (archive) công thức, tìm kiếm toàn văn bản. |
| 2 | **Chung Thiện Ý** 👨‍💻 | `2312804@dlu.edu.vn` | Tham gia xuyên suốt các module; phụ trách Google OAuth & refresh token, danh sách danh mục, tạo/publish công thức & quản lý nguyên liệu, lọc công thức, health check, Welcome Email job. |
| 3 | **Nguyễn Ngọc Bảo Thịnh** 👨‍💻 | `2312757@dlu.edu.vn` | Tham gia xuyên suốt các module; phụ trách đăng xuất & xem hồ sơ, tạo danh mục, chi tiết/xóa công thức & quản lý ảnh, sắp xếp kết quả tìm kiếm, xóa file MinIO, structured logging. |
| 4 | **Hồ Quốc Tiến** 👨‍💻 | `2312769@dlu.edu.vn` | Tham gia xuyên suốt các module; phụ trách cập nhật hồ sơ, cập nhật danh mục, cập nhật công thức & quản lý các bước thực hiện, phân trang & distributed tracing, upload file, sinh thumbnail/sitemap. |

<a id="sec-2"></a>
## 🏗️ 2. Kiến trúc Hệ thống & Công nghệ

Hệ thống vận hành theo kiến trúc **API-Driven** độc lập giữa Frontend và Backend, tuân thủ nguyên tắc Clean Architecture và áp dụng mô hình CQRS kết hợp MediatR Pipeline để tách biệt luồng đọc/ghi.

| Thành phần | Công nghệ sử dụng | Mục đích / Chức năng chính |
| :--- | :--- | :--- |
| ⚙️ **Backend API** | .NET 10 Minimal APIs, C# | Xử lý logic nghiệp vụ, bảo mật và cung cấp REST API. |
| 🎨 **Frontend** | Next.js 15, TypeScript | Hiển thị giao diện UI/UX, kết xuất Server-Side Rendering (SSR). |
| 🗄️ **Cơ sở dữ liệu** | PostgreSQL 16 | Lưu trữ dữ liệu quan hệ, xử lý Full-Text Search (tsvector). |
| ⚡ **Cache & Session** | Redis 7 | Distributed cache hỗ trợ tăng tốc độ đọc và Rate Limiting. |
| ☁️ **Lưu trữ tệp tin** | MinIO (S3-Compatible) | Quản lý Object Storage cho hình ảnh với các kích thước khác nhau. |
| 🕒 **Background Jobs** | Hangfire | Xử lý hàng đợi bất đồng bộ: gửi email, tự sinh thumbnail ảnh, sinh sitemap. |
| 📊 **Giám sát (Monitor)** | Serilog, OpenTelemetry | Ghi log theo cấu trúc, health check và theo dõi hệ thống phân tán (tracing). |

**🐳 Cấu hình Môi trường Triển khai (Docker Compose):**

| Yêu cầu phần cứng | Môi trường Development (Local) | Môi trường Production |
| :--- | :--- | :--- |
| 🧠 **Bộ nhớ (RAM)** | Tối thiểu 8 GB để chạy cụm Docker | Tối thiểu 4 GB cho từng instance |
| 💻 **Vi xử lý (CPU)** | 2 Cores | 2 vCPU |
| 💾 **Lưu trữ (SSD)** | 20 GB | Tối thiểu 50 GB |
| 🛠️ **Nền tảng** | Node.js 20+, .NET 10 SDK, Docker Desktop | Nginx 1.24+, Ubuntu/Debian Linux |

<a id="sec-3"></a>
## 🛠️ 3. Môi trường & Công cụ Cần thiết để Chạy Dự án

### 3.1. Công cụ cần cài đặt (Development)

| Công cụ | Phiên bản tối thiểu | Mục đích |
| :--- | :--- | :--- |
| **.NET SDK** | 10.0+ | Build & chạy Backend API |
| **Node.js** | 20+ (LTS) | Build & chạy Frontend Next.js |
| **Docker Desktop** (hoặc Docker Engine + Compose plugin) | Bản mới nhất | Chạy hạ tầng: PostgreSQL, Redis, MinIO, Seq, MailHog |
| **Git** | 2.40+ | Quản lý mã nguồn: `main` do Quang phụ trách, 3 người còn lại làm trên nhánh cá nhân `mssv_hoten_nhom` (xem Mục 8) |
| **PostgreSQL client** (pgAdmin / DBeaver / `psql`) | — | Xem & kiểm tra dữ liệu trực tiếp trong DB |
| **IDE** | VS Code / Visual Studio 2022+ / JetBrains Rider | Viết code Backend & Frontend |
| **Postman** hoặc **Scalar UI** (`/scalar` — có sẵn khi chạy API) | — | Test API thủ công |

> Yêu cầu phần cứng tối thiểu (RAM/CPU/Storage) cho môi trường Development và Production đã có ở Mục 2 (bảng "Cấu hình Môi trường Triển khai").

### 3.2. Biến môi trường (`.env`) cần cấu hình

| Biến môi trường | Mô tả | Ví dụ (Development) |
| :--- | :--- | :--- |
| `ConnectionStrings__DefaultConnection` | Chuỗi kết nối PostgreSQL | `Host=localhost;Port=5432;Database=culinaryblog;Username=postgres;Password=postgres` |
| `Redis__ConnectionString` | Kết nối Redis (Output Cache) | `localhost:6379` |
| `MinIO__Endpoint` / `MinIO__AccessKey` / `MinIO__SecretKey` / `MinIO__BucketName` | Kết nối Object Storage MinIO | `localhost:9000` / `minioadmin` / `minioadmin` / `recipe-images` |
| `Jwt__SecretKey` / `Jwt__Issuer` | Khóa ký & issuer cho JWT (HS256) | Chuỗi bí mật ngẫu nhiên ≥ 32 ký tự |
| `Google__ClientId` / `Google__ClientSecret` | OAuth Client cho đăng nhập Google | Lấy từ Google Cloud Console |
| `Smtp__Host` / `Smtp__Port` / `Smtp__Username` / `Smtp__Password` | Gửi email (Welcome Email) | Dev: MailHog `localhost` / `1025` (không cần auth) |
| `Seq__ServerUrl` | Server nhận log tập trung | `http://localhost:5341` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint nhận trace/metrics (OpenTelemetry) | Dev: trỏ về Seq OTLP |

### 3.3. Hướng dẫn khởi động nhanh (Quick Start)

1. `git clone <repo-url> && cd CulinaryBlog`
2. Copy `.env.example` → `.env`, điền các biến ở Mục 3.2
3. Khởi động hạ tầng: `docker compose up -d postgres redis minio seq mailhog`
4. Chạy migration: `dotnet ef database update --project src/Infrastructure`
5. Seed dữ liệu mẫu (≥ 20 categories, ≥ 100 recipes — theo yêu cầu Lab 2 ở Mục 7): `dotnet run --project src/Api -- --seed`
6. Chạy Backend API: `dotnet run --project src/Api` (mặc định `https://localhost:5001`, API docs tại `/scalar`)
7. Chạy Frontend: `cd frontend && npm install && npm run dev` (mặc định `http://localhost:3000`)
8. Kiểm tra hệ thống: mở `GET /health` (phải trả `Healthy`), đăng nhập thử bằng tài khoản Admin mẫu do seeder tạo

### 3.4. Cổng (Ports) sử dụng

| Service | Port | Ghi chú |
| :--- | :--- | :--- |
| Frontend (Next.js) | `3000` | — |
| Backend API | `5000` / `5001` | HTTP / HTTPS; Hangfire Dashboard tại `/hangfire` |
| PostgreSQL | `5432` | — |
| Redis | `6379` | — |
| MinIO | `9000` (API) / `9001` (Console) | — |
| Seq | `5341` (ingest) / `8080` (UI) | — |
| MailHog | `1025` (SMTP) / `8025` (UI) | Chỉ dùng ở Development |

<a id="sec-4"></a>
## ⚖️ 4. Các Mâu thuẫn đã Giải quyết trong SRS

> 📄 **Nguồn:** trích/tóm tắt từ [`spec/phan-tich-mau-thuan-SRS-v1.0.0.md`](spec/phan-tich-mau-thuan-SRS-v1.0.0.md) — xem file đó để đọc phân tích đầy đủ (có trích dẫn chương/trang của [`SRS_Culinary_Blog_v1.0.0.pdf`](spec/SRS_Culinary_Blog_v1.0.0.pdf) gốc cho từng mục).

Trong quá trình đọc và phân tích SRS v1.0.0, nhóm phát hiện **11 điểm mâu thuẫn nội bộ** giữa các chương/phụ lục ảnh hưởng trực tiếp đến việc code (ví dụ: Recipe dùng soft delete hay hard delete). Bảng dưới đây tổng hợp từng mâu thuẫn, phương án đã chọn để thống nhất khi code, và lý do lựa chọn. Đây là căn cứ cho SRS v1.1.0 (bản đã hiệu chỉnh) mà toàn bộ Mục 4–6 của tài liệu này tuân theo.

| # | Mâu thuẫn | Phương án đã chọn | Lý do chọn |
| :-: | :--- | :--- | :--- |
| 1 | **Recipe: soft delete hay hard delete?** — FR-RCP-007 mô tả xóa vật lý, nhưng NFR-REL-003 lại yêu cầu đánh dấu `IsDeleted` để có thể khôi phục. | Xóa mềm ngay lập tức (`IsDeleted = true`); dữ liệu + file ảnh trên MinIO chỉ bị xóa cứng sau **30 ngày** bởi job `PurgeDeletedRecipesJob` (Hangfire, mã mới FR-JOB-004). | Vừa khớp NFR-REL-003 (khôi phục được khi xóa nhầm), vừa tránh database phình vô hạn vì không giữ lại vĩnh viễn công thức đã xóa. |
| 2 | **Category: soft delete hay hard delete?** — Không có NFR riêng nhưng cách hiểu rời rạc giữa các phần. | Xóa mềm (`IsDeleted = true`), dùng chung cơ chế với Recipe. | Đồng bộ hành vi giữa các entity, tái sử dụng cùng một Global Query Filter, và cho phép khôi phục danh mục xóa nhầm. |
| 3 | **3 công nghệ cache khác nhau cùng lúc** — `IMemoryCache` (Category), Output Cache (Recipe) và Redis (kiến trúc) được đề cập song song, trong khi NFR-SCALE-001 lại cấm `IMemoryCache`. | Dùng **1 tầng cache duy nhất**: ASP.NET Core Output Cache middleware, backed bởi Redis (custom `IOutputCacheStore`); bỏ hẳn `IMemoryCache` và `CachingBehavior`/`CacheInvalidationBehavior` khỏi pipeline MediatR. | NFR-SCALE-001 yêu cầu backend stateless để scale ngang — `IMemoryCache` là cache theo từng instance, sẽ không đồng bộ khi chạy nhiều instance; hợp nhất về 1 tầng giúp code đơn giản, dễ kiểm soát invalidation. |
| 4 | **TTL cache không khớp nhau** giữa Chương 3 (mô tả rời rạc) và NFR-PERF-003 (số liệu đo lường cụ thể). | Lấy số liệu của NFR-PERF-003 làm chuẩn: Category list 30 phút, Recipe detail 5 phút, Search results 1 phút. | NFR-PERF-003 là yêu cầu phi chức năng có tiêu chí đo lường/kiểm thử rõ ràng (Redis hit rate ≥ 80%), đáng tin hơn mô tả tự do ở chương 3. |
| 5 | **Mã lỗi HTTP khi xung đột RowVersion**: FR-RCP-004 nói trả **409**, nhưng Phụ lục A/B lại ghi **422**. | Thống nhất dùng **409 Conflict** cho mọi xung đột concurrency; sửa lại Phụ lục A/B. | 409 đúng ngữ nghĩa HTTP cho xung đột trạng thái tài nguyên (RFC 9110); 422 chỉ nên dùng cho lỗi validate dữ liệu đầu vào, không phù hợp với concurrency conflict. |
| 6 | **Xử lý trùng slug Recipe**: Phụ lục B / Category thì tự thêm hậu tố, còn FR-RCP-003 lại quy định trả lỗi **409**. | Áp dụng **tự động thêm hậu tố** cho Recipe giống Category (ví dụ `pho-bo-2`); bỏ nhánh trả lỗi 409 do trùng slug. | Trải nghiệm người dùng tốt hơn (không bị chặn khi đặt tên trùng), đồng thời đồng bộ cơ chế với Category đã áp dụng. |
| 7 | **Điều kiện để publish công thức**: FR-RCP-005 chỉ yêu cầu có bước thực hiện (`steps > 0`), nhưng mã lỗi `RECIPE_PUBLISH_INCOMPLETE` ở Phụ lục B lại ngụ ý cần cả nguyên liệu. | Yêu cầu **cả hai**: ≥ 1 `RecipeStep` **và** ≥ 1 `RecipeIngredient` mới cho publish. | Một công thức không có nguyên liệu thì không thể coi là hoàn chỉnh; cách này khớp với mã lỗi đã định nghĩa sẵn ở Phụ lục B. |
| 8 | **Luồng Google OAuth**: FR-AUTH-003 mô tả Authorization Code Flow + PKCE, nhưng đặc tả API (mục 8.1) lại chỉ nhận `idToken`. | Dùng **Authorization Code Flow + PKCE** (Auth.js v5 ở frontend); sửa lại body request backend nhận `{email, displayName, avatarUrl, providerKey}` thay vì `{idToken}`. | Đây là flow mặc định và an toàn hơn của Auth.js v5 (thư viện nhóm chọn) — không để lộ token nhạy cảm ở phía client như flow chỉ dùng idToken. |
| 9 | **Tên trường người dùng không nhất quán**: Chương 3 dùng `fullName`/`userName`, còn Data Model/API lại dùng `displayName`; DTO trả về cũng không đồng nhất (có nơi thiếu `bio`/`emailConfirmed`/`createdAt`). | Chuẩn hóa dùng **`displayName`** ở mọi nơi hiển thị; `userName` chỉ giữ làm định danh đăng nhập nội bộ của Identity; thống nhất `UserProfileDto = {id, email, displayName, avatarUrl, bio, roles, emailConfirmed, createdAt}`. | Tránh nhầm lẫn giữa "tên đăng nhập" và "tên hiển thị"; một DTO duy nhất giúp frontend và backend không bị lệch field khi phát triển song song. |
| 10 | **Cột `Recipe.Instructions`**: được khai báo NOT NULL trong Data Model, nhưng đặc tả request lại đánh dấu field này là optional (`instructions?`). | Đổi cột thành **nullable**. | `Instructions` được ghi chú là field "legacy" — nội dung thật của công thức đã chuyển sang bảng `RecipeStep`; bắt buộc NOT NULL sẽ chặn nhầm các request hợp lệ không còn dùng field cũ. |
| 11 | **`RefreshToken` có kế thừa `BaseEntity` không?** — Phần mô tả chung nói mọi entity kế thừa `BaseEntity`, nhưng bảng cột của `RefreshToken` lại thiếu `IsDeleted`/`UpdatedAt`/`RowVersion`. | Giữ nguyên code: `RefreshToken` **không** kế thừa `BaseEntity`; chỉnh lại câu mô tả chung ở mục 6.4/Chương 7 để nêu rõ đây là trường hợp ngoại lệ. | `RefreshToken` là bản ghi audit dạng append-only (không cần soft delete hay update) — thêm các cột của `BaseEntity` vào đây là dư thừa và không có ý nghĩa. |

<a id="sec-5"></a>
## 📋 5. Yêu cầu Chức năng (Functional Requirements)

Toàn bộ hệ thống có **34 FR**, chia thành **7 module chức năng** theo Chương 3 của [`SRS_Culinary_Blog_v1.0.0.pdf`](spec/SRS_Culinary_Blog_v1.0.0.pdf) (bản v1.1.0 sau khi hiệu chỉnh theo [Mục 4](#sec-4)).

| Mã Module | Tên Module | Số FR | Mô tả tóm tắt |
| :--- | :--- | :-: | :--- |
| **FR-AUTH** | Xác thực & Người dùng | 7 | Đăng ký, đăng nhập email/mật khẩu, đăng nhập Google OAuth 2.0 (Authorization Code + PKCE), refresh token (rotation), đăng xuất, xem/cập nhật hồ sơ. |
| **FR-CAT** | Quản lý Danh mục | 5 | Xem danh sách/chi tiết, tạo/cập nhật/xóa danh mục (Admin), soft delete. |
| **FR-RCP** | Quản lý Công thức | 10 | Danh sách (phân trang/lọc/sắp xếp), chi tiết, tạo/cập nhật, publish/archive, xóa (soft delete), quản lý ảnh/nguyên liệu/các bước. |
| **FR-SRCH** | Tìm kiếm & Phân trang | 4 | Full-Text Search tiếng Việt (PostgreSQL tsvector + unaccent), lọc, sắp xếp, phân trang. |
| **FR-FILE** | Quản lý Tệp tin | 2 | Upload và xóa ảnh trên MinIO (presigned URL). |
| **FR-JOB** | Background Jobs | 3 | Welcome Email, sinh Thumbnail, sinh Sitemap XML (Hangfire Recurring Job). |
| **FR-OBS** | Quan sát Hệ thống | 3 | Health Check Endpoints, Structured Logging (Serilog), Distributed Tracing & Metrics (OpenTelemetry). |
| | **Tổng cộng** | **34** | |

Chi tiết endpoint và các hàm/lớp cần cài đặt cho từng FR trong mỗi module:

### FR-AUTH — Xác thực & Người dùng

| Mã FR | Chức năng | Endpoint | Các hàm / lớp cần cài đặt |
| :-: | :--- | :--- | :--- |
| FR-AUTH-001 | Đăng ký tài khoản | `POST /auth/register` | `RegisterCommand`, `RegisterCommandHandler`, `RegisterCommandValidator` (FluentValidation), `UserManager.CreateAsync` |
| FR-AUTH-002 | Đăng nhập email/mật khẩu | `POST /auth/login` | `LoginCommand`, `LoginCommandHandler`, `SignInManager.CheckPasswordSignInAsync`, `IJwtTokenGenerator.GenerateAccessToken/GenerateRefreshToken` |
| FR-AUTH-003 | Đăng nhập Google OAuth | `POST /auth/google` | `GoogleLoginCommand`, `GoogleLoginCommandHandler`, `UserManager.FindByEmailAsync`/`CreateAsync` (tạo/liên kết user), `IJwtTokenGenerator` |
| FR-AUTH-004 | Làm mới Access Token (refresh + rotation) | `POST /auth/refresh` | `RefreshTokenCommand`, `RefreshTokenCommandHandler`, `IRefreshTokenRepository.GetByTokenAsync/RevokeAsync/RevokeFamilyAsync` |
| FR-AUTH-005 | Đăng xuất | `POST /auth/logout` | `LogoutCommand`, `LogoutCommandHandler`, `IRefreshTokenRepository.RevokeAsync` |
| FR-AUTH-006 | Xem hồ sơ cá nhân | `GET /auth/me` | `GetProfileQuery`, `GetProfileQueryHandler`, `UserManager.FindByIdAsync` (map sang `UserProfileDto`) |
| FR-AUTH-007 | Cập nhật hồ sơ cá nhân | `PATCH /auth/me` | `UpdateProfileCommand`, `UpdateProfileCommandHandler`, `UpdateProfileCommandValidator` |

### FR-CAT — Quản lý Danh mục

| Mã FR | Chức năng | Endpoint | Các hàm / lớp cần cài đặt |
| :-: | :--- | :--- | :--- |
| FR-CAT-001 | Xem danh sách danh mục | `GET /categories` | `GetCategoriesQuery`, `GetCategoriesQueryHandler`, `ICategoryRepository.GetAllAsync`, `IOutputCacheStore` (TTL 30p) |
| FR-CAT-002 | Xem chi tiết danh mục (kèm recipe) | `GET /categories/{id}` | `GetCategoryByIdQuery`, `GetCategoryByIdQueryHandler`, `ICategoryRepository.GetByIdWithRecipesAsync` |
| FR-CAT-003 | Tạo danh mục mới | `POST /categories` [Admin] | `CreateCategoryCommand`, `CreateCategoryCommandHandler`, `CreateCategoryCommandValidator`, `ISlugGenerator.GenerateUniqueAsync` |
| FR-CAT-004 | Cập nhật danh mục | `PUT /categories/{id}` [Admin] | `UpdateCategoryCommand`, `UpdateCategoryCommandHandler`, `ISlugGenerator.GenerateUniqueAsync` |
| FR-CAT-005 | Xóa danh mục | `DELETE /categories/{id}` [Admin] | `DeleteCategoryCommand`, `DeleteCategoryCommandHandler` (set `IsDeleted = true`) |

### FR-RCP — Quản lý Công thức

| Mã FR | Chức năng | Endpoint | Các hàm / lớp cần cài đặt |
| :-: | :--- | :--- | :--- |
| FR-RCP-001 | Danh sách công thức (phân trang/lọc/sắp xếp) | `GET /recipes` | `GetRecipesQuery`, `GetRecipesQueryHandler`, `IRecipeRepository.GetPagedAsync` |
| FR-RCP-002 | Chi tiết công thức | `GET /recipes/{id}` | `GetRecipeByIdQuery`, `GetRecipeByIdQueryHandler`, `IRecipeRepository.GetByIdWithDetailsAsync`, `IncrementViewCountAsync` |
| FR-RCP-003 | Tạo công thức mới | `POST /recipes` [Author/Admin] | `CreateRecipeCommand`, `CreateRecipeCommandHandler`, `CreateRecipeCommandValidator`, `ISlugGenerator.GenerateUniqueAsync` |
| FR-RCP-004 | Cập nhật thông tin cơ bản | `PUT /recipes/{id}` [Author-Owner/Admin] | `UpdateRecipeCommand`, `UpdateRecipeCommandHandler`, `RecipeAuthorizationHandler` (resource ownership), xử lý `RowVersion` concurrency |
| FR-RCP-005 | Publish công thức | `PATCH /recipes/{id}/publish` | `PublishRecipeCommand`, `PublishRecipeCommandHandler` (validate ≥1 step & ≥1 ingredient) |
| FR-RCP-006 | Hủy publish / Lưu trữ (Archive) | `PATCH /recipes/{id}/archive` | `ArchiveRecipeCommand`, `ArchiveRecipeCommandHandler` |
| FR-RCP-007 | Xóa công thức | `DELETE /recipes/{id}` | `DeleteRecipeCommand`, `DeleteRecipeCommandHandler` (set `IsDeleted = true`) |
| FR-RCP-008 | Quản lý ảnh công thức | `POST /recipes/{id}/images`, `PATCH /recipes/{id}/images/{imageId}/primary`, `DELETE /recipes/{id}/images/{imageId}` | `AddRecipeImageCommand`, `SetPrimaryImageCommand`, `DeleteRecipeImageCommand` + handler tương ứng, `IFileStorageService.GeneratePresignedUploadUrlAsync` |
| FR-RCP-009 | Quản lý nguyên liệu | `POST /recipes/{id}/ingredients`, `PUT /recipes/{id}/ingredients/{ingredientId}`, `DELETE /recipes/{id}/ingredients/{ingredientId}` | `AddRecipeIngredientCommand`, `UpdateRecipeIngredientCommand`, `DeleteRecipeIngredientCommand` + handler tương ứng |
| FR-RCP-010 | Quản lý các bước thực hiện | `POST /recipes/{id}/steps`, `PUT /recipes/{id}/steps/{stepId}`, `DELETE /recipes/{id}/steps/{stepId}` | `AddRecipeStepCommand`, `UpdateRecipeStepCommand`, `DeleteRecipeStepCommand` + handler tương ứng |

### FR-SRCH — Tìm kiếm & Phân trang

| Mã FR | Chức năng | Endpoint | Các hàm / lớp cần cài đặt |
| :-: | :--- | :--- | :--- |
| FR-SRCH-001 | Tìm kiếm toàn văn bản | `GET /recipes/search?q=` | `SearchRecipesQuery`, `SearchRecipesQueryHandler`, `IRecipeSearchRepository.SearchAsync` (`tsvector`/`tsquery` + `unaccent`) |
| FR-SRCH-002 | Lọc công thức | `GET /recipes/search?category=&difficulty=&...` | Tham số lọc tích hợp vào `SearchRecipesQuery`/`GetRecipesQuery`, xử lý trong handler tương ứng |
| FR-SRCH-003 | Sắp xếp kết quả (sort=-field) | `GET /recipes?sort=` | `SortParser.Parse` (helper dùng chung), áp dụng trong `GetRecipesQueryHandler`/`SearchRecipesQueryHandler` |
| FR-SRCH-004 | Phân trang | `GET .../?page=&pageSize=` | `PaginatedList<T>.CreateAsync` (helper dùng chung cho mọi Query trả danh sách) |

### FR-FILE — Quản lý Tệp tin

| Mã FR | Chức năng | Endpoint | Các hàm / lớp cần cài đặt |
| :-: | :--- | :--- | :--- |
| FR-FILE-001 | Upload file lên MinIO | `POST /files/presigned-url` | `GetPresignedUploadUrlQuery`, `GetPresignedUploadUrlQueryHandler`, `IFileStorageService.GeneratePresignedUploadUrlAsync`, kiểm tra MIME (magic bytes) |
| FR-FILE-002 | Xóa file khỏi MinIO | `DELETE /files/{key}` | `DeleteFileCommand`, `DeleteFileCommandHandler`, `IFileStorageService.DeleteAsync` |

### FR-JOB — Background Jobs

| Mã FR | Chức năng | Endpoint | Các hàm / lớp cần cài đặt |
| :-: | :--- | :--- | :--- |
| FR-JOB-001 | Welcome Email | *(không có REST endpoint — job nội bộ, trigger từ `RegisterCommandHandler`)* | `WelcomeEmailJob.SendAsync`, `IEmailSender.SendAsync` (MailKit) |
| FR-JOB-002 | Sinh Thumbnail | *(không có REST endpoint — job nội bộ, trigger sau khi upload ảnh)* | `GenerateThumbnailJob.ExecuteAsync`, `IImageProcessor.Resize` |
| FR-JOB-003 | Sinh Sitemap | `GET /sitemap.xml` (kết quả job) | `GenerateSitemapJob.ExecuteAsync` (Hangfire Recurring Job), `ISitemapBuilder.Build`, gọi ping Google Search Console |

### FR-OBS — Quan sát Hệ thống

| Mã FR | Chức năng | Endpoint | Các hàm / lớp cần cài đặt |
| :-: | :--- | :--- | :--- |
| FR-OBS-001 | Health Check Endpoints | `GET /health`, `GET /health/live`, `GET /health/ready` | `AddHealthChecks()` (Postgres/Redis/MinIO check), `MapHealthChecks(...)`, có thể thêm `CustomHealthCheck` riêng cho MinIO |
| FR-OBS-002 | Structured Logging | *(không có endpoint riêng — middleware toàn cục)* | `CorrelationIdMiddleware.InvokeAsync`, `LoggingBehavior<TRequest,TResponse>.Handle` (MediatR pipeline) |
| FR-OBS-003 | Distributed Tracing & Metrics | *(không có endpoint riêng — instrumentation toàn cục)* | Cấu hình `AddAspNetCoreInstrumentation()`/`AddEntityFrameworkCoreInstrumentation()` (OpenTelemetry), custom `ActivitySource` cho metrics nghiệp vụ (recipe created/published) |

<a id="sec-6"></a>
## 🛡️ 6. Yêu cầu Phi Chức năng (Non-Functional Requirements)

Theo Chương 4 của [`SRS_Culinary_Blog_v1.0.0.pdf`](spec/SRS_Culinary_Blog_v1.0.0.pdf), mô hình hóa theo ISO/IEC 25010 (FURPS+) — **29 NFR** chia thành 7 nhóm.

| Mã NFR | Danh mục | Số yêu cầu | Ưu tiên | Điểm nổi bật |
| :--- | :--- | :-: | :-: | :--- |
| **NFR-PERF** | Hiệu năng | 5 | Cao | API p95 ≤ 500ms, p99 ≤ 1s; ≥ 100 concurrent users; Redis cache hit ≥ 80% (Category 30p / Recipe 5p / Search 1p TTL); Core Web Vitals LCP ≤ 2.5s. |
| **NFR-SEC** | Bảo mật | 6 | Rất cao | Hash password PBKDF2 (≥100k iteration); JWT HS256 15 phút + Refresh Token rotation 7 ngày; Rate limit 10 req/phút cho `/auth/*`; input validation + kiểm tra magic bytes khi upload; HTTPS + CORS whitelist; kiểm tra resource ownership ở Application Layer. |
| **NFR-USE** | Khả năng sử dụng | 4 | Trung bình | Responsive 320px–≥1200px; WCAG 2.1 AA; lỗi trả theo RFC 7807 Problem Details; loading skeleton/optimistic update. |
| **NFR-REL** | Độ tin cậy | 3 | Cao | Uptime ≥ 99.5%; Global Exception Handler; **Recipe dùng soft delete (IsDeleted), không xóa vật lý**; backup PostgreSQL hàng ngày. |
| **NFR-MAINT** | Khả năng bảo trì | 4 | Trung bình | Static analysis bắt buộc (SonarAnalyzer/ESLint); Unit test ≥ 80% coverage; API docs tự sinh (Scalar); tuân thủ nghiêm Clean Architecture (kiểm tra bằng ArchUnit.NET). |
| **NFR-SCALE** | Khả năng mở rộng | 3 | Cao | Backend stateless (JWT, **Redis** thay cho `IMemoryCache`); connection pooling PostgreSQL; hạ tầng Docker scale ngang, Nginx load balancer. |
| **NFR-SEO** | Tối ưu SEO | 4 | Cao | JSON-LD Schema.org Recipe markup; Open Graph & meta tags chuẩn; sitemap.xml tự sinh mỗi ngày (FR-JOB-003), ping Google Search Console. |
| | **Tổng cộng** | **29** | | |

<a id="sec-7"></a>
## 📅 7. Kế hoạch & Phân công Công việc Chi tiết theo Buổi

Quá trình phát triển dự án kéo dài **8 buổi**: Buổi 1–2 cả nhóm cùng làm (đọc SRS, dựng module Auth nền tảng), Buổi 3–7 mỗi FR được giao cho 1 người phụ trách chính (đủ Database → API → Frontend cho phần việc của mình), Buổi 8 cả nhóm tích hợp & deploy. Tổng khối lượng: **Quang 7 FR · Thiện Ý 9 FR · Bảo Thịnh 9 FR · Quốc Tiến 9 FR** (34 FR).

> 📄 **Nguồn:** trích/tóm tắt từ [`spec/phan_cong_chi_tiet_theo_buoi.md`](spec/phan_cong_chi_tiet_theo_buoi.md) — xem file đó để đọc bảng phân công gốc theo Tuần → Buổi.

### Buổi 1 — Đọc và phân tích SRS (cả nhóm) ✅ Xong

| Thành viên | Công việc | Cách làm | Trạng thái |
| :--- | :--- | :--- | :-: |
| Mai Văn Quang | Đọc toàn bộ SRS v1.1.0; chủ trì họp kiến trúc | Đọc 71 trang SRS; họp chốt kiến trúc Clean Architecture + CQRS/MediatR; khởi tạo repo, khung docker-compose, khung Next.js chung | ✅ Xong |
| Chung Thiện Ý | Đọc SRS phần Auth, Category, Recipe | Ghi chú các mâu thuẫn đã rà soát (soft/hard delete, slug, concurrency, publish) | ✅ Xong |
| Nguyễn Ngọc Bảo Thịnh | Đọc SRS phần Category, Recipe, Search | Ghi chú NFR-PERF (cache TTL) và NFR-SEC liên quan | ✅ Xong |
| Hồ Quốc Tiến | Đọc SRS phần Auth, File, Job, Observability | Ghi chú NFR-REL-003 (soft delete), NFR-SCALE-001 (Redis thay IMemoryCache) | ✅ Xong |

---

### Buổi 2 — Module Xác thực & Người dùng (FR-AUTH-001, FR-AUTH-002) ✅ Xong

#### Yêu cầu tối thiểu Lab 2 (giáo viên) — Nền tảng Backend

| # | Yêu cầu (giáo viên) | Người phụ trách | Cách làm / Hướng dẫn triển khai | Trạng thái |
| :-: | :--- | :-: | :--- | :-: |
| 1 | Hoàn thành tạo cấu trúc dự án backend theo Clean Architecture | Quang | Tạo 4 project trong solution: **Domain** (entities/enums, không phụ thuộc layer khác), **Application** (CQRS commands/queries, interfaces, DTOs — chỉ phụ thuộc Domain), **Infrastructure** (EF Core, Identity, Redis, MinIO — implement interface của Application), **Presentation/API** (Minimal API endpoints, DI). Tuân thủ dependency rule của NFR-MAINT-004. | ✅ Đã hoàn thành |
| 2 | Hoàn thành cài đặt các gói thư viện cần thiết | Quang | Cài `MediatR`, `FluentValidation`, `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Serilog.AspNetCore` + `Serilog.Sinks.Seq`, `Hangfire.Core/AspNetCore/PostgreSql`, `AWSSDK.S3` (cho MinIO), `Bogus` (sinh dữ liệu giả), `Scalar`/Swashbuckle cho API docs. | ✅ Đã hoàn thành |
| 3 | Hoàn thành việc cài đặt các lớp entities, configuration, dbcontext | Quang | Viết entity (`ApplicationUser`, `Category`, `Recipe`, `RecipeStep`, `RecipeIngredient`, `RefreshToken`...); mỗi entity có `IEntityTypeConfiguration<T>` riêng (Fluent API: khóa ngoại, index, ràng buộc unique cho slug); `AppDbContext` kế thừa `IdentityDbContext`, đăng ký configuration qua `ApplyConfigurationsFromAssembly`. | ✅ Đã hoàn thành |
| 4 | Hoàn thành việc tạo migration, cài đặt các lớp để tạo dữ liệu ngẫu nhiên | Quang | Chạy `dotnet ef migrations add InitialCreate` rồi `dotnet ef database update`; viết `DataSeeder`/`FakeDataGenerator` dùng `Bogus` để sinh dữ liệu giả cho Category, Recipe, RecipeIngredient, RecipeStep. | ✅ Đã hoàn thành |
| 5 | Đảm bảo CSDL có dữ liệu ngẫu nhiên: ≥ 20 categories, ≥ 100 recipes (mỗi recipe ≥ 10 nguyên liệu, ≥ 5 bước chế biến) | Quang | Chạy seeder rồi kiểm tra bằng `psql`/pgAdmin: `SELECT COUNT(*) FROM "Categories"` (≥ 20), `SELECT COUNT(*) FROM "Recipes"` (≥ 100), và `GROUP BY`/`HAVING COUNT(*) < 10` trên `RecipeIngredients`, `< 5` trên `RecipeSteps` để đảm bảo không có recipe nào thiếu dữ liệu. | ✅ Đã hoàn thành |

#### Module FR-AUTH

**Mục tiêu:** có đủ đường vào hệ thống (đăng ký/đăng nhập email + mật khẩu), sẵn sàng để làm nền JWT cho các phần còn lại.

**Tiến trình trong buổi:**
1. Quang dựng `RegisterCommand`/`LoginCommand` và `IJwtTokenGenerator`.
2. Áp nghị quyết **MT-09** chuẩn hóa `UserProfileDto`/`displayName` trước khi viết các FR còn lại của module (tránh phải sửa DTO nhiều lần về sau).

**Commit nền — Mai Văn Quang dẫn · MT-09 (chuẩn hóa `UserProfileDto`/`displayName`)**

- **Cách làm:** xóa `fullName`/`userName` khỏi mọi DTO trả về; tạo `UserProfileDto { id, email, displayName, avatarUrl, bio, roles, emailConfirmed, createdAt }` dùng chung cho FR-AUTH-006/007; `userName` chỉ còn là cột nội bộ của `ApplicationUser` (định danh đăng nhập Identity), không xuất hiện trong response nào.

- **Vì sao:** nếu để mỗi người tự đặt tên field DTO, khả năng cao 2 người dùng 2 tên khác nhau cho cùng khái niệm "tên hiển thị" — frontend phải viết 2 kiểu map dữ liệu và dễ lệch nhau khi code song song.

- **Xong khi:** `GetProfileQuery` và `UpdateProfileCommand` đều dùng chung một class `UserProfileDto` duy nhất.

- **Commit:** `refactor(auth): unify UserProfileDto with displayName per SRS v1.1.0 MT-09`

**Mai Văn Quang · FR-AUTH-001 Đăng ký + FR-AUTH-002 Đăng nhập**

- **Cách làm:** `RegisterCommand` + `RegisterCommandValidator` (FluentValidation): email đúng định dạng và chưa tồn tại; mật khẩu ≥ 8 ký tự, có hoa/thường/số/ký tự đặc biệt (NFR-SEC-001). Gọi `UserManager.CreateAsync` — Identity tự hash bằng PBKDF2-HMACSHA512, không tự viết hàm hash tay. `LoginCommand`: `SignInManager.CheckPasswordSignInAsync` xác thực; đúng thì `IJwtTokenGenerator.GenerateAccessToken` (JWT HS256, claim userId/email/roles/jti, TTL 15 phút) và `GenerateRefreshToken` (random 128-bit, hash SHA-256 trước khi lưu bảng `RefreshTokens`, TTL 7 ngày). Endpoint: `POST /auth/register` trả 201 + `UserProfileDto` (không có password); `POST /auth/login` trả `{accessToken, refreshToken}` hoặc 401.

- **Vì sao:** dùng `UserManager`/`SignInManager` có sẵn thay vì tự so sánh hash để tránh lặp lại lỗi bảo mật kinh điển (so sánh không constant-time, chọn iteration count quá thấp...); lưu refresh token dạng hash để lộ DB cũng không dùng lại được token thật.

- **Xong khi:** đăng ký/đăng nhập chạy được qua Scalar UI; đăng ký email đã tồn tại trả lỗi rõ ràng (không phải 500); đăng nhập sai mật khẩu trả đúng 401.

- **Commit:** `feat(auth): implement FR-AUTH-001 register and FR-AUTH-002 login with JWT issuance`

**Kiểm chứng cuối Buổi 2:** đăng ký tài khoản mới thành công, trả về `UserProfileDto` không có password; đăng nhập đúng mật khẩu nhận được `{accessToken, refreshToken}`; đăng nhập sai mật khẩu trả đúng 401; đăng ký lại email đã tồn tại trả lỗi rõ ràng (không phải 500).

---

### Buổi 3 — Module Quản lý Danh mục (FR-CAT, 5 FR)

**Mục tiêu:** Admin quản trị được danh mục đầy đủ CRUD, có cache đúng chuẩn NFR-PERF-003, và Category sẵn sàng cho Recipe tham chiếu ở Buổi 4.

**Tiến trình trong buổi:**
1. Quang dựng trước tầng Output Cache + Redis (áp **MT-03**, **MT-04**) vì cả 4 FR đọc/ghi của buổi này đều cần dùng chung một tầng cache.
2. Bảo Thịnh làm FR-CAT-003 trước (tạo danh mục) để có dữ liệu mẫu cho Thiện Ý/Quang test FR-CAT-001/002.
3. Quốc Tiến làm FR-CAT-004 sau khi `ISlugGenerator` của Bảo Thịnh merge xong (tái sử dụng lại).
4. Quang làm FR-CAT-005 cuối buổi, áp **MT-02** (soft delete).
5. Cuối buổi: gộp nhánh, kiểm tra cache bị xóa đúng sau mỗi lệnh ghi.

**Commit nền — Mai Văn Quang dẫn · MT-03 & MT-04 (hợp nhất tầng cache)**

- **Cách làm:** viết `RedisOutputCacheStore : IOutputCacheStore`; cấu hình `AddOutputCache` với policy riêng cho `categories` (TTL 30 phút) và `recipes` (TTL 5 phút, dùng ở Buổi 4); xóa hẳn code `IMemoryCache` còn sót trong khung dự án từ Buổi 1; bỏ `CachingBehavior`/`CacheInvalidationBehavior` khỏi pipeline MediatR nếu có.

- **Vì sao:** NFR-SCALE-001 cấm `IMemoryCache` vì nó không đồng bộ giữa nhiều instance API; dựng cache một lần ở đây để các FR còn lại (và cả Recipe/Search sau này) chỉ cần gắn `[OutputCache(PolicyName = "...")]` chứ không ai phải tự viết lại cơ chế cache.

- **Xong khi:** gọi `GET /categories` hai lần liên tiếp, lần hai có dấu hiệu cache-hit rõ rệt; tắt Redis thì API vẫn chạy (không cache) chứ không crash.

- **Commit:** `feat(infra): unify caching layer to redis output cache per SRS v1.1.0 MT-03 MT-04`

**Chung Thiện Ý · FR-CAT-001 Xem danh sách**

- **Cách làm:** `GetCategoriesQuery`/Handler gọi `ICategoryRepository.GetAllAsync` (chỉ lấy `IsDeleted = false`); gắn `[OutputCache(PolicyName = "categories")]` lên `GET /categories`.

- **Vì sao:** danh sách danh mục gần như không đổi trong ngày nên TTL 30 phút (theo MT-04) là hợp lý, giảm tải DB đáng kể cho trang chủ vốn gọi API này liên tục.

- **Xong khi:** `GET /categories` trả danh sách đúng, không có danh mục đã xóa mềm; lần gọi thứ hai trong 30 phút nhanh hơn rõ rệt.

- **Commit:** `feat(categories): implement FR-CAT-001 list with output cache`

**Mai Văn Quang · FR-CAT-002 Xem chi tiết (kèm recipe)**

- **Cách làm:** `GetCategoryByIdQuery` join sang `Recipes` theo `CategoryId`, chỉ lấy recipe đã publish (`Status = Published`); dùng `PaginatedList<T>` (định nghĩa sẵn để Buổi 6 dùng lại cho Search).

- **Vì sao:** hiển thị cả recipe chưa publish trong trang danh mục công khai sẽ lộ nội dung nháp của tác giả khác — phải lọc status ngay ở query, không lọc ở frontend.

- **Xong khi:** `GET /categories/{id}` trả đúng thông tin danh mục kèm recipe đã publish, phân trang hoạt động; trả 404 nếu `id` không tồn tại hoặc đã xóa mềm.

- **Commit:** `feat(categories): implement FR-CAT-002 detail with published recipes`

**Nguyễn Ngọc Bảo Thịnh · FR-CAT-003 Tạo danh mục**

- **Cách làm:** `CreateCategoryCommand` [Admin] + Validator (`name` 2–100 ký tự, `imageUrl` optional); kiểm tra tên trùng trước khi ghi; viết `ISlugGenerator.GenerateUniqueAsync` sinh slug từ tên theo cách tái sử dụng được cho cả Recipe ở Buổi 4 (đúng tinh thần MT-06 — đồng bộ cơ chế).

- **Vì sao:** dựng `ISlugGenerator` dùng chung ngay từ Category để Buổi 4 không phải viết lại logic tự thêm hậu tố cho Recipe.

- **Xong khi:** Admin tạo được danh mục mới với slug hợp lệ; user thường gọi endpoint bị 403; đặt tên trùng bị chặn ở tầng Application, không rơi xuống lỗi DB 500.

- **Commit:** `feat(categories): implement FR-CAT-003 create with reusable slug generator`

**Hồ Quốc Tiến · FR-CAT-004 Cập nhật danh mục**

- **Cách làm:** `UpdateCategoryCommand` [Admin]; nếu đổi tên khiến slug trùng danh mục khác thì gọi lại `ISlugGenerator.GenerateUniqueAsync` để tự thêm hậu tố (`-2`, `-3`...) thay vì trả lỗi.

- **Vì sao:** áp đúng nghị quyết MT-06 (đã chọn tự thêm hậu tố thay vì chặn 409) — trải nghiệm Admin không bị gián đoạn khi đặt trùng tên.

- **Xong khi:** cập nhật tên trùng danh mục khác vẫn lưu thành công, slug tự đổi; cache `categories:*` bị xóa ngay sau khi cập nhật.

- **Commit:** `feat(categories): implement FR-CAT-004 update with slug auto-suffix`

**Mai Văn Quang · FR-CAT-005 Xóa danh mục**

- **Cách làm:** áp MT-02: `DeleteCategoryCommand` [Admin] chỉ set `IsDeleted = true`; thêm Global Query Filter (`HasQueryFilter(c => !c.IsDeleted)`) trong `CategoryConfiguration` để mọi query khác tự động ẩn danh mục đã xóa mà không ai phải nhớ tự lọc.

- **Vì sao:** Global Query Filter là cách an toàn nhất để đảm bảo "xóa mềm" thực sự ẩn dữ liệu ở mọi nơi — nếu để từng handler tự lọc `IsDeleted`, chỉ cần quên một chỗ là dữ liệu đã xóa lại lộ ra.

- **Xong khi:** `DELETE /categories/{id}` xong thì danh mục biến mất khỏi mọi endpoint đọc, nhưng vẫn còn trong DB (kiểm tra trực tiếp bằng `psql`).

- **Commit:** `feat(categories): implement FR-CAT-005 soft delete with global query filter`

**Kiểm chứng cuối Buổi 3:** 5 FR-CAT chạy được end-to-end qua Scalar; cache tự xóa đúng sau Create/Update/Delete; slug không bao giờ trùng nhau trong bảng `Categories`.

---

### Buổi 4 — Module Công thức (phần lõi) + Background Job (6 FR)

**Mục tiêu:** Author tạo được công thức (ở trạng thái nháp), sửa được an toàn dưới tranh chấp đồng thời (concurrency), và publish đúng điều kiện; ảnh upload tự sinh thumbnail.

**Tiến trình trong buổi:**
1. Commit nền do Thiện Ý dẫn: áp **MT-10** (migration `Instructions` → nullable) trước khi ai tạo recipe test, tránh lỗi DB do field NOT NULL cũ.
2. Thiện Ý làm FR-RCP-003 (tạo) trước để có dữ liệu cho Bảo Thịnh/Quang test FR-RCP-001/002.
3. Quốc Tiến làm FR-RCP-004 (áp **MT-05** — 409 Conflict) song song.
4. Thiện Ý làm tiếp FR-RCP-005 (publish, áp **MT-07**) sau khi RCP-003 merge.
5. Quốc Tiến làm JOB-002 cuối buổi (cần ảnh upload thật để test resize).

**Commit nền — Chung Thiện Ý dẫn · MT-10 (`Instructions` → nullable)**

- **Cách làm:** migration `AlterColumn` đổi `Recipes.Instructions` từ NOT NULL sang nullable; cập nhật `RecipeConfiguration` (`IsRequired(false)`); `CreateRecipeCommand` không còn bắt buộc field này trong request.

- **Vì sao:** `Instructions` là field legacy — nội dung thật của công thức nằm ở `RecipeStep` (Buổi 5); giữ NOT NULL sẽ chặn nhầm mọi request hợp lệ không còn dùng field cũ này.

- **Xong khi:** `dotnet ef database update` chạy sạch; tạo recipe không gửi `instructions` vẫn thành công.

- **Commit:** `fix(recipes): make instructions column nullable per SRS v1.1.0 MT-10`

**Mai Văn Quang · FR-RCP-001 Danh sách công thức**

- **Cách làm:** `GetRecipesQuery` hỗ trợ `page`/`pageSize`, lọc `categoryId`/`difficulty`, `sort`; chỉ trả recipe `Status = Published` cho người dùng ẩn danh, Author xem thêm được recipe Draft của chính mình; gắn `[OutputCache(PolicyName = "recipes-list", Duration = 300)]`.

- **Vì sao:** nếu không tách rule hiển thị theo vai trò ngay từ query gốc, Search (Buổi 6) tái sử dụng lại chung logic sẽ lặp đúng lỗi lộ bản nháp.

- **Xong khi:** `GET /recipes` trả đúng danh sách theo bộ lọc; ẩn danh không thấy recipe Draft của người khác.

- **Commit:** `feat(recipes): implement FR-RCP-001 paginated list with cache`

**Nguyễn Ngọc Bảo Thịnh · FR-RCP-002 Chi tiết công thức**

- **Cách làm:** `GetRecipeByIdQuery` trả đầy đủ `steps`, `ingredients`, ảnh, thông tin `Category`/tác giả; tăng `ViewCount` bằng một lệnh `UPDATE` riêng (không qua EF change tracking) để không tranh chấp `RowVersion` với `UpdateRecipeCommand` của FR-RCP-004.

- **Vì sao:** nếu tăng view count qua cùng entity đang được lệnh update sửa, hai thao tác đọc/ghi có thể tranh chấp concurrency token giả — tách thành lệnh SQL riêng để không ảnh hưởng token thật.

- **Xong khi:** `GET /recipes/{id}` trả đủ dữ liệu cho trang chi tiết; `ViewCount` tăng đúng mỗi lượt xem, không làm hỏng cơ chế 409 của FR-RCP-004.

- **Commit:** `feat(recipes): implement FR-RCP-002 detail view with safe view-count increment`

**Chung Thiện Ý · FR-RCP-003 Tạo công thức mới**

- **Cách làm:** `CreateRecipeCommand` [Author/Admin] + Validator (`title` 5–200, `description` 20–2000, `prepTime`/`cookTime` > 0, `categoryId` phải tồn tại); recipe luôn tạo ở trạng thái `Draft`; slug qua `ISlugGenerator.GenerateUniqueAsync` (tái sử dụng từ Buổi 3) — trùng thì tự thêm hậu tố (MT-06), không trả 409.

- **Vì sao:** luôn tạo Draft để có một điểm chặn publish duy nhất ở FR-RCP-005, thay vì kiểm tra điều kiện rải rác nhiều nơi.

- **Xong khi:** `POST /recipes` tạo recipe Draft thành công; đặt tên trùng recipe khác vẫn tạo được (slug tự đổi).

- **Commit:** `feat(recipes): implement FR-RCP-003 create draft recipe with slug auto-suffix per MT-06`

**Hồ Quốc Tiến · FR-RCP-004 Cập nhật thông tin cơ bản**

- **Cách làm:** `UpdateRecipeCommand` [Author-Owner/Admin] dùng `RecipeAuthorizationHandler` kiểm tra quyền sở hữu; gửi kèm `RowVersion` nhận từ lần đọc trước; bắt `DbUpdateConcurrencyException` khi `SaveChanges` → map sang **409 Conflict** (áp MT-05, không dùng 422 như Phụ lục cũ).

- **Vì sao:** 409 đúng ngữ nghĩa HTTP cho xung đột trạng thái tài nguyên; nếu vẫn dùng 422, frontend sẽ nhầm đây là lỗi validate dữ liệu và hiển thị sai thông báo.

- **Xong khi:** hai tab cùng sửa một recipe, tab lưu sau nhận đúng 409 kèm thông báo "dữ liệu đã bị người khác thay đổi".

- **Commit:** `feat(recipes): implement FR-RCP-004 update with optimistic concurrency returning 409 per MT-05`

**Chung Thiện Ý · FR-RCP-005 Publish công thức**

- **Cách làm:** `PublishRecipeCommand` kiểm tra `Steps.Count >= 1 && Ingredients.Count >= 1` (áp MT-07, hợp nhất yêu cầu cũ) trước khi đổi `Status = Published`; thiếu điều kiện thì trả lỗi `RECIPE_PUBLISH_INCOMPLETE`.

- **Vì sao:** một công thức không có nguyên liệu thì không thể coi là hoàn chỉnh — khớp đúng mã lỗi đã định nghĩa sẵn ở Phụ lục B của SRS.

- **Xong khi:** publish recipe đủ điều kiện thành công; thiếu step hoặc ingredient bị chặn với thông báo rõ ràng, không phải lỗi 500.

- **Commit:** `feat(recipes): implement FR-RCP-005 publish with step+ingredient validation per MT-07`

**Hồ Quốc Tiến · FR-JOB-002 Sinh Thumbnail**

- **Cách làm:** sau khi ảnh được upload (endpoint của Buổi 5), enqueue `BackgroundJob.Enqueue<GenerateThumbnailJob>` qua Hangfire; job dùng `IImageProcessor.Resize` sinh 3 kích thước (small/medium/large), lưu lại MinIO cạnh ảnh gốc.

- **Vì sao:** resize ảnh tốn CPU và không cần chặn response của request upload — đẩy vào job nền để API trả về nhanh.

- **Xong khi:** upload một ảnh xong, sau vài giây thấy thêm các file thumbnail trong bucket MinIO mà không cần gọi API riêng.

- **Commit:** `feat(jobs): implement FR-JOB-002 thumbnail generation triggered after image upload`

**Kiểm chứng cuối Buổi 4:** tạo → sửa (2 tab cùng lúc ra đúng 409) → publish (thiếu ingredient bị chặn) chạy trơn tru; ảnh test upload thủ công sinh đúng thumbnail.

---

### Buổi 5 — Module Công thức (ảnh/nguyên liệu/xóa) + Job + Observability (8 FR)

**Mục tiêu:** công thức có đầy đủ vòng đời (archive/xóa mềm), quản lý được ảnh/nguyên liệu/bước, sitemap tự sinh, và hệ thống có health check + log có cấu trúc để debug từ đây trở đi.

**Tiến trình trong buổi:**
1. Bảo Thịnh dẫn commit nền **MT-01** (soft delete Recipe + job purge) trước khi làm FR-RCP-007.
2. Thiện Ý (nguyên liệu) và Quốc Tiến (bước) làm song song vì hai entity độc lập nhau.
3. Bảo Thịnh làm ảnh (RCP-008) sau khi dùng lại `IFileStorageService` đã có từ Buổi 4.
4. Quốc Tiến làm JOB-003 (sitemap) cuối buổi khi đã có đủ recipe Published để test.
5. Thiện Ý/Bảo Thịnh làm Observability (OBS-001/002) song song, độc lập với phần Recipe.

**Commit nền — Nguyễn Ngọc Bảo Thịnh dẫn · MT-01 (soft delete Recipe + FR-JOB-004 purge)**

- **Cách làm:** `DeleteRecipeCommand` chỉ set `IsDeleted = true` (không xóa vật lý); thêm Global Query Filter cho `Recipe` (giống Category ở Buổi 3); định nghĩa job mới `PurgeDeletedRecipesJob` (FR-JOB-004, Hangfire Recurring Job chạy hàng ngày) xóa cứng + gọi `IFileStorageService.DeleteAsync` cho ảnh liên quan của các recipe đã `IsDeleted = true` quá 30 ngày.

- **Vì sao:** NFR-REL-003 yêu cầu khôi phục được khi xóa nhầm, nhưng giữ mãi mãi sẽ phình database — xóa mềm + purge định kỳ giải quyết cả hai vấn đề cùng lúc.

- **Xong khi:** xóa recipe xong thì nó biến mất khỏi mọi danh sách nhưng còn trong DB; trigger thủ công `PurgeDeletedRecipesJob` qua Hangfire dashboard xóa đúng các recipe quá hạn và ảnh MinIO tương ứng.

- **Commit:** `feat(recipes): implement FR-RCP-007 soft delete and FR-JOB-004 purge job per MT-01`

**Mai Văn Quang · FR-RCP-006 Hủy publish / Lưu trữ**

- **Cách làm:** `ArchiveRecipeCommand` [Author-Owner/Admin] chuyển `Status` sang `Archived`; recipe archived bị loại khỏi `GetRecipesQuery`/Search công khai nhưng vẫn hiện trong "công thức của tôi" của tác giả.

- **Vì sao:** tách rõ "archive" (tác giả chủ động ẩn tạm) khỏi "xóa mềm" (MT-01) — hai trạng thái có ý nghĩa nghiệp vụ khác nhau, không nên dùng chung một cờ `IsDeleted`.

- **Xong khi:** `PATCH /recipes/{id}/archive` chuyển trạng thái đúng; recipe archived không xuất hiện ở trang chủ nhưng tác giả vẫn quản lý được.

- **Commit:** `feat(recipes): implement FR-RCP-006 archive/unpublish`

**Nguyễn Ngọc Bảo Thịnh · FR-RCP-008 Quản lý ảnh công thức**

- **Cách làm:** `AddRecipeImageCommand` dùng `IFileStorageService.GeneratePresignedUploadUrlAsync`, ảnh đầu tiên tự động là ảnh chính; `SetPrimaryImageCommand` đổi ảnh chính bằng 1 transaction 2 bước (bỏ cờ chính ảnh cũ → gán cờ chính ảnh mới) vì DB có unique index "chỉ 1 ảnh chính mỗi recipe"; `DeleteRecipeImageCommand` xóa ảnh — nếu xóa đúng ảnh chính thì ảnh có `OrderIndex` nhỏ nhất tự lên thay.

- **Vì sao:** đổi ảnh chính bằng một lệnh `UPDATE` duy nhất dễ vi phạm unique index nếu thứ tự ghi không đảm bảo — tách hai bước trong cùng transaction để luôn có đúng một ảnh chính tại mọi thời điểm.

- **Xong khi:** thêm/xóa/đổi ảnh chính hoạt động đúng qua nhiều lần liên tiếp mà không vi phạm unique index.

- **Commit:** `feat(recipes): implement FR-RCP-008 image management with safe primary-image swap`

**Chung Thiện Ý · FR-RCP-009 Quản lý nguyên liệu**

- **Cách làm:** `AddRecipeIngredientCommand`/`UpdateRecipeIngredientCommand`/`DeleteRecipeIngredientCommand`, mỗi `RecipeIngredient` có `SortOrder`; xóa/thêm xong tính lại `SortOrder` liên tục để FR-RCP-005 (đếm `Count >= 1`) luôn đúng và UI hiển thị đúng thứ tự.

- **Vì sao:** publish (MT-07) phụ thuộc trực tiếp vào số lượng ingredient — CRUD nguyên liệu phải giữ dữ liệu nhất quán để điều kiện publish không bị sai lệch.

- **Xong khi:** thêm/sửa/xóa nguyên liệu phản ánh đúng thứ tự hiển thị; recipe đủ ≥ 1 ingredient publish được, xóa hết thì publish bị chặn lại.

- **Commit:** `feat(recipes): implement FR-RCP-009 ingredient management`

**Hồ Quốc Tiến · FR-RCP-010 Quản lý các bước + FR-JOB-003 Sinh Sitemap**
- **Cách làm (RCP-010):** tương tự nguyên liệu — CRUD `RecipeStep` có `SortOrder`, mỗi bước có thể đính một ảnh minh họa riêng (dùng lại `IFileStorageService`).
- **Cách làm (JOB-003):** `GenerateSitemapJob` (Hangfire Recurring Job, 02:00 AM hàng ngày) build `sitemap.xml` từ toàn bộ recipe/category `Published`, ping Google Search Console qua HTTP GET.

- **Vì sao:** gộp hai việc trong cùng buổi vì cả hai đều cần dữ liệu recipe Published thật để test — làm RCP-010 trước để có bước thực hiện đầy đủ, rồi publish thử vài recipe để JOB-003 có dữ liệu sinh sitemap.

- **Xong khi:** CRUD bước hoạt động đúng thứ tự; truy cập `/sitemap.xml` thấy đủ URL các trang đã publish.

- **Commit:** `feat(recipes): implement FR-RCP-010 step management; feat(jobs): implement FR-JOB-003 sitemap generation`

**Chung Thiện Ý · FR-OBS-001 Health Check Endpoints**

- **Cách làm:** `AddHealthChecks()` đăng ký `AspNetCore.HealthChecks.NpgSql`, `.Redis`, `.Minio`; `/health` tổng hợp cả 3; `/health/live` chỉ trả `Healthy` nếu process còn sống; `/health/ready` fail (503) khi DB hoặc Redis down.

- **Vì sao:** tách liveness/readiness theo đúng chuẩn Kubernetes-style health check — `/health/live` dùng để quyết định có nên khởi động lại container, `/health/ready` dùng để quyết định có nên route traffic vào hay không, hai mục đích khác nhau không nên gộp chung.

- **Xong khi:** tắt thử Postgres, `/health/ready` trả 503; `/health/live` vẫn 200 vì process vẫn sống.

- **Commit:** `feat(observability): implement FR-OBS-001 health check endpoints`

**Nguyễn Ngọc Bảo Thịnh · FR-OBS-002 Structured Logging**

- **Cách làm:** `CorrelationIdMiddleware` gắn `X-Correlation-ID` vào mỗi request (tạo mới nếu client chưa gửi); `LoggingBehavior<TRequest,TResponse>` trong pipeline MediatR log mọi Command/Query kèm thời gian xử lý; Serilog sink ra Console (JSON) + File (rolling daily) + Seq.

- **Vì sao:** gắn logging ở tầng MediatR pipeline (thay vì rải log khắp handler) để không ai quên log khi viết handler mới — mọi Command/Query tự động được log mà không cần code thêm.

- **Xong khi:** mở Seq thấy log có `CorrelationId`, method/path/status, thời gian xử lý cho mọi request; request > 500ms tự có cảnh báo mức Warning.

- **Commit:** `feat(observability): implement FR-OBS-002 structured logging via correlation id and mediatr pipeline`

**Kiểm chứng cuối Buổi 5:** đủ vòng đời Recipe: tạo → publish → archive/xóa mềm; sitemap phản ánh đúng recipe đã publish; `/health/*` và log Seq hoạt động.

---

### Buổi 6 — Module Tìm kiếm & Phân trang + Tracing (5 FR)

**Mục tiêu:** tìm được công thức bằng tiếng Việt có dấu lẫn không dấu, lọc/sắp xếp/phân trang đúng, và có tracing để soi được đường đi của một request qua các layer.

**Tiến trình trong buổi:**
1. Quang làm FR-SRCH-001 trước (dựng `tsvector`/`tsquery`) vì FR-SRCH-002/003/004 đều gắn tham số vào chung một query gốc.
2. Thiện Ý và Bảo Thịnh làm song song (lọc, sắp xếp) trên nhánh của Quang.
3. Quốc Tiến làm FR-SRCH-004 (phân trang, tái sử dụng `PaginatedList<T>` từ Buổi 3) sau cùng để gộp cả 3 tham số vào một response nhất quán.
4. Quốc Tiến làm OBS-003 song song, độc lập (instrumentation toàn cục, không đụng code Search).

**Mai Văn Quang · FR-SRCH-001 Tìm kiếm toàn văn bản**

- **Cách làm:** thêm cột `SearchVector` (`tsvector`) trên `Recipes`, kết hợp extension `unaccent` để bỏ dấu tiếng Việt trước khi so khớp; `SearchRecipesQuery`/Handler dùng `tsquery` (`websearch_to_tsquery` — chấp nhận cú pháp gõ tự nhiên), xếp hạng bằng `ts_rank`.

- **Vì sao:** xử lý bỏ dấu ở tầng PostgreSQL (qua `unaccent`) thay vì ở code C# vì DB có index GIN cho `tsvector`, tốc độ tìm kiếm trên hàng trăm nghìn dòng nhanh hơn nhiều so với lọc chuỗi ở application layer.

- **Xong khi:** gõ "pho bo" (không dấu) vẫn ra kết quả "Phở bò"; kết quả liên quan nhất hiện lên trước.

- **Commit:** `feat(search): implement FR-SRCH-001 full-text search with unaccent + ts_rank`

**Chung Thiện Ý · FR-SRCH-002 Lọc công thức**

- **Cách làm:** thêm tham số `categoryId`, `difficulty`, khoảng `prepTime`/`cookTime` vào `SearchRecipesQuery`; áp từng điều kiện bằng `Where` động, kết hợp được nhiều điều kiện lọc cùng lúc.

- **Vì sao:** gộp lọc vào cùng query tìm kiếm (thay vì làm endpoint riêng) để tránh hai đường trả kết quả khác nhau cho cùng một khái niệm "danh sách công thức thỏa điều kiện".

- **Xong khi:** kết hợp `q` + `categoryId` + `difficulty` cùng lúc trả đúng tập kết quả giao nhau của mọi điều kiện.

- **Commit:** `feat(search): implement FR-SRCH-002 combinable filters`

**Nguyễn Ngọc Bảo Thịnh · FR-SRCH-003 Sắp xếp kết quả**

- **Cách làm:** `SortParser.Parse("-createdAt")` tách dấu `-` (giảm dần) và tên field, whitelist các field được phép sắp xếp; map sang `OrderBy`/`OrderByDescending` động.

- **Vì sao:** whitelist field thay vì nhận thẳng tên cột từ query string — nếu không, người dùng có thể truyền tên cột nội bộ không nên lộ ra, hoặc gây lỗi 500 khi field không tồn tại.

- **Xong khi:** `?sort=-createdAt` trả mới nhất trước; truyền field không hợp lệ trả 400 thay vì 500.

- **Commit:** `feat(search): implement FR-SRCH-003 whitelisted dynamic sorting`

**Hồ Quốc Tiến · FR-SRCH-004 Phân trang + FR-OBS-003 Distributed Tracing & Metrics**
- **Cách làm (SRCH-004):** chuẩn hóa `page`/`pageSize` (giới hạn `pageSize` tối đa), trả kèm `totalCount`/`totalPages`; gắn `[OutputCache(PolicyName = "search", Duration = 60)]` cho response tìm kiếm (theo MT-04).
- **Cách làm (OBS-003, bổ sung theo SRS mục 3.7):** cấu hình OpenTelemetry `AddAspNetCoreInstrumentation()` + `AddEntityFrameworkCoreInstrumentation()`, export OTLP đến Seq (dev)/Grafana Tempo (prod); tạo `ActivitySource` riêng, ghi custom metric đếm recipe created/published.

- **Vì sao:** giới hạn `pageSize` tối đa là chặn một dạng DoS đơn giản; OBS-003 được bổ sung vào buổi này (không có trong bản phân công gốc) vì SRS mục 3.7 quy định module FR-OBS có 3 FR chứ không phải 2.

- **Xong khi:** phân trang trả đúng số liệu; mở Seq/Tempo thấy được trace đầy đủ một request search đi qua API → EF Core → DB.

- **Commit:** `feat(search): implement FR-SRCH-004 pagination with cache; feat(observability): implement FR-OBS-003 distributed tracing per SRS 3.7`

**Kiểm chứng cuối Buổi 6:** tìm kiếm không dấu vẫn ra đúng kết quả; kết hợp lọc + sắp xếp + phân trang cho ra response nhất quán; xem được trace của chính request đó trên Seq.

---

### Buổi 7 — Module Tệp tin + Job Welcome Email (3 FR)

**Mục tiêu:** upload/xóa ảnh an toàn qua MinIO đúng chuẩn bảo mật NFR-SEC-004, và người dùng mới nhận được email chào mừng.

**Tiến trình trong buổi:**
1. Quốc Tiến làm FR-FILE-001 trước (endpoint presigned URL) vì FR-FILE-002 cần biết đúng key đã upload để xóa.
2. Bảo Thịnh làm FR-FILE-002 song song, kết hợp dọn lại các ảnh test đã tạo ở Buổi 4–5.
3. Thiện Ý làm FR-JOB-001 độc lập, không phụ thuộc hai FR còn lại.
4. Cuối buổi: cả nhóm rà lại 34 FR theo cột "Trạng thái", lập danh sách còn thiếu để xử lý đầu Buổi 8.

**Hồ Quốc Tiến · FR-FILE-001 Upload file lên MinIO**

- **Cách làm:** `GetPresignedUploadUrlQuery` dùng `AWSSDK.S3` (endpoint override trỏ về MinIO) sinh presigned URL có hạn dùng ngắn; client upload thẳng lên bucket bằng URL đó, không đi qua backend; sau khi upload, có bước xác minh magic bytes ở phía xử lý ảnh (không tin Content-Type header client khai báo).

- **Vì sao:** presigned URL giúp backend không phải "ôm" luồng dữ liệu file lớn; kiểm tra magic bytes thay vì Content-Type vì client hoàn toàn có thể khai báo sai định dạng để qua mặt validate (NFR-SEC-004).

- **Xong khi:** client lấy được presigned URL và upload ảnh thành công thẳng lên MinIO; upload file đổi đuôi giả `.jpg` (không phải ảnh thật) bị từ chối ở bước xử lý sau.

- **Commit:** `feat(files): implement FR-FILE-001 presigned upload with magic-byte validation`

**Nguyễn Ngọc Bảo Thịnh · FR-FILE-002 Xóa file khỏi MinIO**

- **Cách làm:** `DeleteFileCommand`/`IFileStorageService.DeleteAsync` xóa object theo key; gọi từ 3 nơi: khi ảnh bị gỡ khỏi Recipe/Category (FR-RCP-008), và khi `PurgeDeletedRecipesJob` (MT-01, Buổi 5) chạy dọn recipe quá hạn.

- **Vì sao:** gom logic xóa file vào một service duy nhất (`IFileStorageService`) để không có chỗ nào gọi thẳng SDK MinIO riêng lẻ — dễ audit và dễ đổi provider lưu trữ sau này nếu cần.

- **Xong khi:** gỡ ảnh khỏi recipe thì file cũng biến mất khỏi bucket; chạy thử `PurgeDeletedRecipesJob` xóa đúng ảnh của các recipe đã hết hạn 30 ngày.

- **Commit:** `feat(files): implement FR-FILE-002 delete wired into recipe cleanup and purge job`

**Chung Thiện Ý · FR-JOB-001 Welcome Email**

- **Cách làm:** trong `RegisterCommandHandler` (Buổi 2), sau khi `UserManager.CreateAsync` thành công, `BackgroundJob.Enqueue<WelcomeEmailJob>` (Hangfire); `WelcomeEmailJob.SendAsync` dùng `IEmailSender` (MailKit, SMTP; dev trỏ về MailHog) gửi email chào mừng có tên người dùng.

- **Vì sao:** enqueue thay vì gửi email đồng bộ ngay trong handler đăng ký — nếu SMTP chậm hoặc lỗi, người dùng vẫn đăng ký thành công ngay lập tức, việc gửi email được Hangfire tự retry (tối đa 3 lần, exponential backoff) ở nền.

- **Xong khi:** đăng ký tài khoản mới xong, mở MailHog thấy email chào mừng đến đúng địa chỉ trong vài giây.

- **Commit:** `feat(jobs): implement FR-JOB-001 welcome email enqueued after registration`

**Kiểm chứng cuối Buổi 7:** upload/xóa ảnh hoạt động đúng qua MinIO thật; đăng ký tài khoản mới luôn có email chào mừng trong MailHog; toàn bộ 34 FR đã rà lại trạng thái, FR nào còn thiếu được liệt kê để xử lý đầu Buổi 8.

---

### Buổi 8 — Tích hợp, Kiểm thử, Triển khai (cả nhóm)

| Thành viên | Công việc | Cách làm | Trạng thái |
| :--- | :--- | :--- | :-: |
| Mai Văn Quang | Tích hợp, Test E2E, Deploy | Integration test end-to-end (WebApplicationFactory); viết `docker-compose.prod.yml`, deploy thử; tổng hợp báo cáo dự án. | 📝 Kế hoạch |
| Chung Thiện Ý | Tích hợp, Test E2E, Deploy | Test toàn luồng Auth + Recipe (tạo → publish → xóa); hỗ trợ sửa lỗi khi ghép module. | 📝 Kế hoạch |
| Nguyễn Ngọc Bảo Thịnh | Tích hợp, Test E2E, Deploy | Test Category/Search/File; polish UI/UX frontend, fix bug hiển thị. | 📝 Kế hoạch |
| Hồ Quốc Tiến | Tích hợp, Test E2E, Deploy | Test Job/Observability/File; hỗ trợ tích hợp frontend, chuẩn bị script demo end-to-end. | 📝 Kế hoạch |

<a id="sec-8"></a>
## 🚀 8. Quy tắc làm việc & Triển khai

Nhóm 18 quản lý nhánh theo mô hình đơn giản: **Mai Văn Quang phụ trách nhánh `main`** (review & merge, đảm bảo `main` luôn ở trạng thái chạy được); **3 thành viên còn lại mỗi người làm việc trên đúng 1 nhánh cá nhân** đặt tên theo mẫu `mssv_hoten_nhom` (không tạo thêm nhánh `feature/bugfix` con theo từng việc). Khi cần đưa code vào `main`, tạo **Pull Request (PR)** từ nhánh cá nhân, Quang review rồi mới Merge.

| Thành viên | Vai trò nhánh | Tên nhánh |
| :--- | :--- | :--- |
| **Mai Văn Quang** | Quản lý, review & merge PR | `main` |
| **Chung Thiện Ý** | Làm việc trên nhánh cá nhân | `2312804_ChungThienY_nhom18` |
| **Nguyễn Ngọc Bảo Thịnh** | Làm việc trên nhánh cá nhân | `2312757_NguyenNgocBaoThinh_nhom18` |
| **Hồ Quốc Tiến** | Làm việc trên nhánh cá nhân | `2312769_HoQuocTien_nhom18` |

Nhánh cá nhân không tách loại — mọi thay đổi (tính năng mới, sửa lỗi, tài liệu...) đều commit thẳng trên nhánh `mssv_hoten_nhom` của mình. Việc **phân loại chỉ thể hiện ở message commit**, theo đúng chuẩn Conventional Commits đã dùng xuyên suốt Mục 7 của file này:

| Loại thay đổi | Tiền tố commit | Ví dụ (lấy từ Mục 7) |
| :--- | :--- | :--- |
| ✨ Tính năng mới | `feat(<module>): ...` | `feat(auth): implement FR-AUTH-001 register and FR-AUTH-002 login with JWT issuance` |
| 🐛 Sửa lỗi | `fix(<module>): ...` | `fix(recipes): make instructions column nullable per SRS v1.1.0 MT-10` |
| ♻️ Tái cấu trúc | `refactor(<module>): ...` | `refactor(auth): unify UserProfileDto with displayName per SRS v1.1.0 MT-09` |
| 📄 Tài liệu | `docs: ...` | `docs: update phân công công việc theo buổi` |
