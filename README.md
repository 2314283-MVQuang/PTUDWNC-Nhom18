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

### Buổi 3 (Tuần 3) — Bắt đầu module riêng của từng người ✅ Quang xong · còn lại đang làm

> ⚠️ **Đã cập nhật:** nội dung "Module Quản lý Danh mục (FR-CAT, 5 FR)" chia round-robin cho cả 4 người (bản cũ) không còn đúng thực tế. Nhóm chạy theo mô hình **mỗi người ôm trọn 1 module riêng** (Auth nâng cao/Observability — Recipe — Category — File Upload), khớp với nhánh Git thật trên GitHub. Bảng chi tiết đầy đủ nằm ở [`spec/phan_cong_chi_tiet_theo_buoi.md`](spec/phan_cong_chi_tiet_theo_buoi.md) mục "Tuần 3".

#### Yêu cầu tối thiểu Tuần 3 (giáo viên) — cho mỗi thành viên

| # | Yêu cầu (giáo viên) | Áp dụng ra sao |
| :-: | :--- | :--- |
| 1 | Hoàn thành cài đặt các lớp domain exceptions | Mỗi người tự thêm exception riêng cho module mình (vd `RecipeNotFoundException`, `CategoryNotFoundException`, `InvalidFileException`) |
| 2 | Hoàn thành cài đặt các lớp repository & unit of work | Mỗi người tự viết `I<Module>Repository`/`<Module>Repository` riêng — dùng chung `IUnitOfWork` đã có sẵn từ kiến trúc gốc |
| 3 | Hoàn thành ít nhất 2 API endpoint/thành viên | Tự nhiên đạt với Recipe (3 endpoint)/Category (3 endpoint); File Upload cần chủ động thêm 1 endpoint nữa cho đủ 2 |
| 4 | Middleware bắt lỗi toàn cục trả về problem details | Dùng chung `GlobalExceptionMiddleware` đã có sẵn trên `main` từ Buổi 2 — không ai cần viết lại |

**Mai Văn Quang · Auth nâng cao + Observability** — ✅ Đã hoàn thành

- **Cách làm:** `ChangePasswordCommand`, `ForgotPasswordCommand`/`ResetPasswordCommand`, `ConfirmEmailCommand`; `RoleSeeder` seed role "Admin"/"Author" vào `AspNetRoles` ở MỌI environment (không chỉ Development); Serilog đọc cấu hình từ section `Serilog` (sink Console + Seq); OpenTelemetry instrument HTTP (ASP.NET Core + HttpClient) và SQL (Npgsql), xuất OTLP nếu có collector, ngược lại in ra console.

- **Vì sao:** tách riêng đổi mật khẩu (đã đăng nhập) khỏi quên/đặt lại mật khẩu (chưa đăng nhập được) vì luồng xác thực khác nhau hoàn toàn; `RoleSeeder` phải chạy mọi environment vì thiếu nó thì `AddToRoleAsync(user, "Author")` lúc đăng ký sẽ lỗi ngay cả ở production.

- **Xong khi:** 4 endpoint mới chạy đúng qua Scalar; role "Admin"/"Author" tồn tại sẵn trong DB ngay sau lần khởi động đầu tiên; log có `CorrelationId`, trace xuất được ra Seq (dev) hoặc console.

- **Commit:** `feat(auth): Tuan 3 - Quang lam Auth nang cao (change/forgot/reset password, confirm email, role seeder) + Observability (Serilog->Seq, OpenTelemetry, health checks)`

**Chung Thiện Ý · Recipe CRUD cơ bản (Create/Read)** — ✅ Đã hoàn thành

- **Cần làm:** entity `Recipe`/`RecipeStep`/`RecipeIngredient` + migration; `CreateRecipeCommand`, `GetRecipeByIdQuery`, `GetRecipesQuery`; **bắt buộc thêm** `IRecipeRepository`/`RecipeRepository` (interface này từng có trong scaffold gốc rồi bị gỡ khỏi `main` để Ý tự làm lại) và `RecipeNotFoundException`.

- **Xong khi (mục tiêu):** `POST /api/v1/recipes`, `GET /api/v1/recipes/{id}`, `GET /api/v1/recipes` chạy đúng; gọi id không tồn tại trả lỗi qua `GlobalExceptionMiddleware` (ProblemDetails, không phải 500 thô).

**Nguyễn Ngọc Bảo Thịnh · Category CRUD + soft delete** — ⬜ Đang làm

- **Cần làm:** `CreateCategoryCommand`/`UpdateCategoryCommand`/`DeleteCategoryCommand` (`IsDeleted = true`, không xóa vật lý); slug tự thêm hậu tố khi trùng; **bắt buộc thêm** `ICategoryRepository`/`CategoryRepository` (cũng bị gỡ khỏi `main` cùng lý do trên) và `CategoryNotFoundException`.

- **Xong khi (mục tiêu):** `POST`/`PUT`/`DELETE /api/v1/categories/{id}` chạy đúng; xóa xong category biến mất khỏi `GET /categories` nhưng vẫn còn trong DB.

**Hồ Quốc Tiến · Setup File Upload (MinIO)** — ⬜ Đang làm

- **Cần làm:** tích hợp MinIO SDK .NET, sinh presigned URL cho client upload ảnh trực tiếp, validate loại/kích thước file; **bắt buộc thêm** (module này vốn không có repository tự nhiên): entity `UploadedFile` + `IUploadedFileRepository`/`UploadedFileRepository` lưu metadata mỗi lần cấp presigned URL, `InvalidFileException` khi sai loại/kích thước, và endpoint thứ 2 (`GET /api/v1/files/{id}`) bên cạnh `POST /api/v1/files/presigned-url` để đủ tối thiểu 2 endpoint.

- **Xong khi (mục tiêu):** xin được presigned URL và upload thành công lên MinIO; file sai định dạng/quá lớn bị chặn với lỗi rõ ràng qua middleware chung; metadata file lưu được vào DB.

**Kiểm chứng cuối Buổi 3:** Quang xong 4/4; Ý, Thịnh, Tiến còn lại đang code — kiểm tra lại theo bảng "Yêu cầu tối thiểu" ở trên trước khi báo cáo nhóm.

---

### Buổi 4 (Tuần 4) — Tiếp tục sâu vào module riêng ✅ Xong (cả nhóm)

> ⚠️ **Đã cập nhật:** nội dung "Module Công thức (phần lõi) + Background Job" chia round-robin cho cả 4 người (bản cũ) không còn đúng — mỗi người tiếp tục module riêng đã nhận từ Buổi 3. Bảng chi tiết đầy đủ nằm ở [`spec/phan_cong_chi_tiet_theo_buoi.md`](spec/phan_cong_chi_tiet_theo_buoi.md) mục "Tuần 4".

#### Yêu cầu tối thiểu Tuần 4 (giáo viên) — cho mỗi thành viên

| # | Yêu cầu (giáo viên) | Áp dụng ra sao |
| :-: | :--- | :--- |
| 1 | Hoàn thành cài đặt **tất cả** API endpoint thuộc phạm vi việc được giao tuần này | Không để endpoint nào dở dang (vd làm Add mà bỏ Update/Delete) — xem cột "Endpoint cần hoàn thành đủ" bên dưới |

3 yêu cầu tối thiểu của Tuần 3 (domain exceptions, repository & UnitOfWork, middleware bắt lỗi toàn cục) vẫn tiếp tục áp dụng cho mọi endpoint mới tuần này.

**Mai Văn Quang · Output Cache + Redis (hạ tầng); CI/CD** — ✅ Đã hoàn thành

- **Cần làm:** viết `RedisOutputCacheStore : IOutputCacheStore`; cấu hình `AddOutputCache` với policy riêng cho từng module (Category/Recipe); bỏ hẳn `IMemoryCache`/`CachingBehavior`/`CacheInvalidationBehavior` cũ (mâu thuẫn #3); TTL theo NFR-PERF-003 (mâu thuẫn #4); GitHub Actions build→test→docker push.

- **Đã làm:** Output Cache dùng Redis qua gói chính thức `Microsoft.AspNetCore.OutputCaching.StackExchangeRedis` (`AddStackExchangeRedisOutputCache`, thay cho việc tự viết `RedisOutputCacheStore` — mâu thuẫn #3 cho phép dùng thư viện có sẵn); 3 policy `categories` (30 phút) / `RecipeDetail` (5 phút) / `Search` (1 phút) trong `Program.cs`; xoá cache theo tag khi tạo/sửa/xoá; không còn `IMemoryCache`. CI/CD ở `.github/workflows/ci.yml`: build → test với mọi push/PR vào `main`, sau khi merge vào `main` thì build Docker image API và đẩy lên `ghcr.io`.

- **Lưu ý:** đây là việc hạ tầng/CI, **không phát sinh endpoint mới** — không tính vào yêu cầu "đủ endpoint" của tuần này; 4 endpoint Auth nâng cao của Tuần 3 vẫn giữ nguyên, không có cái nào dở dang.

**Chung Thiện Ý · Recipe Update + concurrency; slug** — ✅ Đã hoàn thành

- **Cần làm:** `UpdateRecipeCommand` [Author-Owner/Admin] dùng `RowVersion` (concurrency token); bắt `DbUpdateConcurrencyException` khi `SaveChanges` → map sang **409 Conflict** (mâu thuẫn #5); slug trùng thì tự thêm hậu tố (mâu thuẫn #6).

- **Endpoint cần hoàn thành đủ:** `PUT /api/v1/recipes/{id}` — endpoint duy nhất tuần này, "tất cả" nghĩa là phải xong endpoint này trọn vẹn kèm đúng 409 khi có tranh chấp.

**Nguyễn Ngọc Bảo Thịnh · Full-text Search** — ✅ Đã hoàn thành

- **Cần làm:** PostgreSQL `tsvector`/`tsquery` + extension `unaccent`; xếp hạng theo `ts_rank`.

- **Endpoint cần hoàn thành đủ:** `GET /api/v1/recipes/search?q=...` — endpoint duy nhất tuần này, phải tìm đúng dù gõ có dấu hay không dấu (vd "pho bo" ra "Phở bò").

**Hồ Quốc Tiến · Recipe: ảnh, nguyên liệu, các bước (FR-RCP-008,009,010)** — ✅ Đã hoàn thành

- **Cần làm:** `RecipeIngredient`/`RecipeStep` CRUD (DB+API), gắn ảnh qua presigned URL đã setup Tuần 3.

- **Endpoint cần hoàn thành đủ (6 endpoint, không được chỉ làm Add mà bỏ dở Update/Delete):** `POST`/`PUT`/`DELETE /api/v1/recipes/{id}/ingredients` và `POST`/`PUT`/`DELETE /api/v1/recipes/{id}/steps`.

**Kiểm chứng cuối Buổi 4 (mục tiêu):** sửa recipe (2 tab cùng lúc ra đúng 409) chạy trơn tru; tìm kiếm có dấu/không dấu ra đúng kết quả; đủ cả 6 endpoint ảnh/nguyên liệu/bước, không endpoint nào dở dang.

---

### Buổi 5 (Tuần 5) — Hoàn tất vòng đời Công thức + Job nền ⬜ Chưa làm (cả nhóm)

> ⚠️ **Đã cập nhật:** bảng Buổi 5 gốc (8 FR) không còn đúng. 5 chức năng của bảng cũ đã làm xong từ Tuần 3–4 nên **không giao lại**: FR-RCP-008 ảnh, FR-RCP-009 nguyên liệu, FR-RCP-010 các bước (Tiến, Tuần 4); FR-OBS-001 health check, FR-OBS-002 structured logging (Quang, Tuần 3). Các chức năng **chưa có trên `main`** được chia lại đều cho Buổi 5–6–7 để buổi nào mỗi người cũng có đúng 1 chức năng chính. Bảng đầy đủ ở [`spec/phan_cong_chi_tiet_theo_buoi.md`](spec/phan_cong_chi_tiet_theo_buoi.md) mục "Tuần 5".

**Mục tiêu:** công thức có đầy đủ vòng đời (publish → archive / xóa mềm), sitemap tự sinh, job dọn dữ liệu chạy định kỳ.

**Tiến trình trong buổi:**
1. Thiện Ý làm FR-RCP-005 (publish) trước — Quang và Tiến cần có recipe `Published` để test.
2. Bảo Thịnh cài Hangfire + làm FR-RCP-007 (soft delete) và job purge; Tiến dùng lại cấu hình Hangfire đó.
3. Quang làm FR-RCP-006 (archive) song song với Thịnh vì hai lệnh độc lập nhau.
4. Quốc Tiến làm FR-JOB-003 (sitemap) cuối buổi khi đã có recipe Published.

**Mai Văn Quang · FR-RCP-006 Hủy publish / Lưu trữ** — ⬜ Chưa làm

- **Cách làm:** `ArchiveRecipeCommand` [Author-Owner/Admin] chuyển `Status` sang `Archived`; recipe archived bị loại khỏi `GetRecipesQuery`/Search công khai nhưng vẫn hiện trong "công thức của tôi" của tác giả; xoá cache tag liên quan sau khi đổi trạng thái.

- **Vì sao:** tách rõ "archive" (tác giả chủ động ẩn tạm) khỏi "xóa mềm" (MT-01) — hai trạng thái có ý nghĩa nghiệp vụ khác nhau, không nên dùng chung một cờ `IsDeleted`.

- **Xong khi:** `PATCH /recipes/{id}/archive` chuyển trạng thái đúng; recipe archived không xuất hiện ở trang chủ nhưng tác giả vẫn quản lý được.

- **Commit:** `feat(recipes): implement FR-RCP-006 archive/unpublish`

**Chung Thiện Ý · FR-RCP-005 Publish công thức** — ⬜ Chưa làm

- **Cách làm:** `PublishRecipeCommand` [Author-Owner/Admin] gọi `Recipe.Publish()`; domain method kiểm tra `Steps.Count > 0 && Ingredients.Count > 0` (mâu thuẫn #7), thiếu thì ném exception map sang `RECIPE_PUBLISH_INCOMPLETE` (400) kèm thông báo rõ thiếu gì; set `PublishedAt`.

- **Vì sao:** recipe published được index SEO với JSON-LD (`recipeIngredient[]` bắt buộc) — thiếu nguyên liệu thì structured data sai.

- **Xong khi:** `PATCH /recipes/{id}/publish` chuyển `Draft` → `Published`; recipe thiếu bước hoặc nguyên liệu bị chặn.

- **Commit:** `feat(recipes): implement FR-RCP-005 publish with step and ingredient check`

**Nguyễn Ngọc Bảo Thịnh · FR-RCP-007 Xóa công thức (MT-01) + FR-JOB-004 purge** — ⬜ Chưa làm

- **Cách làm:** `DeleteRecipeCommand` chỉ set `IsDeleted = true` (không xóa vật lý); cài Hangfire; `PurgeDeletedRecipesJob` (Recurring Job chạy hàng ngày) xóa cứng + gọi `IFileStorageService` xóa ảnh của các recipe đã `IsDeleted = true` quá 30 ngày.

- **Vì sao:** NFR-REL-003 yêu cầu khôi phục được khi xóa nhầm, nhưng giữ mãi mãi sẽ phình database — xóa mềm + purge định kỳ giải quyết cả hai.

- **Xong khi:** xóa recipe xong thì nó biến mất khỏi mọi danh sách và tìm kiếm nhưng còn trong DB; chạy tay `PurgeDeletedRecipesJob` qua Hangfire dashboard xóa đúng các recipe quá hạn.

- **Commit:** `feat(recipes): implement FR-RCP-007 soft delete and FR-JOB-004 purge job per MT-01`

**Hồ Quốc Tiến · FR-JOB-003 Sinh Sitemap** — ⬜ Chưa làm

- **Cách làm:** `GenerateSitemapJob` (Hangfire Recurring Job, 02:00 AM hàng ngày) build `sitemap.xml` từ toàn bộ recipe/category `Published`.

- **Vì sao:** sitemap cần dữ liệu recipe Published thật để test nên làm sau FR-RCP-005.

- **Xong khi:** truy cập `/sitemap.xml` thấy đủ URL các trang đã publish.

- **Commit:** `feat(jobs): implement FR-JOB-003 sitemap generation`

**Kiểm chứng cuối Buổi 5:** đủ vòng đời Recipe: tạo → publish → archive / xóa mềm; sitemap phản ánh đúng recipe đã publish.

---

### Buổi 6 (Tuần 6) — Hoàn thiện Tìm kiếm (lọc, sắp xếp, phân trang) + Chi tiết Danh mục ⬜ Chưa làm (cả nhóm)

> ⚠️ **Đã cập nhật:** FR-SRCH-001 (tìm toàn văn bản) Thịnh đã làm ở Tuần 4 và FR-OBS-003 (tracing) Quang đã làm ở Tuần 3 nên **không giao lại**. Quang nhận FR-CAT-002 (đang làm dở: mới có xem theo ID, chưa kèm công thức).

**Mục tiêu:** kết quả tìm kiếm lọc/sắp xếp/phân trang được, và trang danh mục hiển thị được công thức thuộc danh mục đó.

**Tiến trình trong buổi:**
1. Ý, Thịnh, Tiến cùng sửa `SearchRecipesQuery` — thống nhất chữ ký tham số trước khi code.
2. Merge theo thứ tự Ý (lọc) → Thịnh (sắp xếp) → Tiến (phân trang) để tránh xung đột.
3. Quang làm FR-CAT-002 song song, độc lập với phần Search.

**Mai Văn Quang · FR-CAT-002 Chi tiết danh mục kèm công thức** — ⬜ Chưa làm

- **Cách làm:** `GetCategoryBySlugQuery` tìm category theo slug, trả `CategoryDetailDto` kèm `PagedResult<RecipeSummaryDto>` chỉ gồm recipe `Published`; gắn Output Cache (tag `categories`); đồng thời gắn policy `RecipeDetail` (đã khai báo trong `Program.cs` nhưng chưa dùng) cho `GET /recipes/{slug}`.

- **Vì sao:** hiện `GET /categories/{id}` chỉ trả thông tin danh mục, chưa đúng SRS (tra theo slug, kèm danh sách công thức phân trang) — trang danh mục ở frontend cần đúng dữ liệu này.

- **Xong khi:** `GET /categories/{slug}?page=&pageSize=` trả `{ category, recipes: { items, totalCount, page, pageSize, totalPages } }`; slug sai trả 404 dạng ProblemDetails.

- **Commit:** `feat(categories): implement FR-CAT-002 category detail with paged recipes`

**Chung Thiện Ý · FR-SRCH-002 Lọc kết quả tìm kiếm** — ⬜ Chưa làm

- **Cách làm:** thêm tham số `categoryId`, `difficulty`, khoảng `prepTime`/`cookTime` vào `SearchRecipesQuery`; áp từng điều kiện bằng `Where` động, kết hợp được nhiều điều kiện lọc cùng lúc.

- **Vì sao:** gộp lọc vào cùng query tìm kiếm (thay vì làm endpoint riêng) để tránh hai đường trả kết quả khác nhau cho cùng một khái niệm "danh sách công thức thỏa điều kiện".

- **Xong khi:** kết hợp `q` + `categoryId` + `difficulty` cùng lúc trả đúng tập kết quả giao nhau của mọi điều kiện.

- **Commit:** `feat(search): implement FR-SRCH-002 combinable filters`

**Nguyễn Ngọc Bảo Thịnh · FR-SRCH-003 Sắp xếp kết quả tìm kiếm** — ⬜ Chưa làm

- **Cách làm:** `SortParser.Parse("-createdAt")` tách dấu `-` (giảm dần) và tên field, whitelist các field được phép sắp xếp; map sang `OrderBy`/`OrderByDescending` động.

- **Vì sao:** whitelist field thay vì nhận thẳng tên cột từ query string — nếu không, người dùng có thể truyền tên cột nội bộ không nên lộ ra, hoặc gây lỗi 500 khi field không tồn tại.

- **Xong khi:** `?sort=-createdAt` trả mới nhất trước; truyền field không hợp lệ trả 400 thay vì 500.

- **Commit:** `feat(search): implement FR-SRCH-003 whitelisted dynamic sorting`

**Hồ Quốc Tiến · FR-SRCH-004 Phân trang kết quả tìm kiếm** — ⬜ Chưa làm

- **Cách làm:** thay giới hạn cứng 50 dòng trong `SearchRecipesAsync` bằng `page`/`pageSize` (giới hạn `pageSize` tối đa), trả `PagedResult` kèm `totalCount`/`totalPages`; giữ policy cache `Search` 1 phút (mâu thuẫn #4).

- **Vì sao:** giới hạn `pageSize` tối đa là chặn một dạng DoS đơn giản; dùng lại `PagedResult<T>` để response tìm kiếm cùng dạng với `GET /recipes`.

- **Xong khi:** kết quả tìm kiếm phân trang đúng số liệu; kết hợp được với lọc và sắp xếp.

- **Commit:** `feat(search): implement FR-SRCH-004 pagination for search results`

**Kiểm chứng cuối Buổi 6:** kết hợp `q` + lọc + sắp xếp + phân trang cho ra response nhất quán; trang danh mục hiện đúng công thức đã publish.

---

### Buổi 7 (Tuần 7) — Module Tệp tin + Job nền còn lại + nối Frontend Auth ⬜ Chưa làm (cả nhóm)

> ⚠️ **Đã cập nhật:** FR-FILE-001 (upload MinIO) Tiến đã làm ở Tuần 3–4 nên **không giao lại**; Tiến nhận FR-JOB-002 (chưa ai làm). Quang nhận việc nối frontend đăng ký/đăng nhập với backend thật.

**Mục tiêu:** xóa ảnh sạch khỏi MinIO, có ảnh thu nhỏ và email chào mừng chạy nền, và giao diện đăng ký/đăng nhập dùng tài khoản thật trong database.

**Tiến trình trong buổi:**
1. Bốn việc độc lập nhau, làm song song; Ý và Tiến dùng lại Hangfire đã cài từ Buổi 5.
2. Cuối buổi: cả nhóm rà lại 34 FR theo cột "Trạng thái", lập danh sách còn thiếu để xử lý đầu Buổi 8.

**Mai Văn Quang · FR-AUTH-001/002 (frontend) Nối đăng ký/đăng nhập với backend** — ⬜ Chưa làm

- **Cách làm:** frontend hiện kiểm tra danh sách user cục bộ (`frontend/data/users.json`, `lib/local-users.ts`) trước rồi mới gọi backend, và cấp token giả `local-token-...`; chuyển `auth.ts` và form đăng ký sang gọi thẳng `POST /auth/register`, `POST /auth/login`; form đăng ký nhập email và mật khẩu theo đúng policy của backend.

- **Vì sao:** token giả không qua được xác thực JWT của backend nên người dùng đăng nhập trên giao diện không gọi được API cần quyền; SRS yêu cầu tài khoản lưu trong PostgreSQL.

- **Xong khi:** tài khoản đăng ký trên giao diện xuất hiện trong bảng `AspNetUsers`; đăng nhập xong gọi được API cần quyền bằng JWT thật.

- **Commit:** `feat(auth): wire frontend register/login to backend API`

**Chung Thiện Ý · FR-JOB-001 Welcome Email + bổ sung health check MinIO** — ⬜ Chưa làm

- **Cách làm:** trong `RegisterCommandHandler` (Buổi 2), sau khi `UserManager.CreateAsync` thành công, `BackgroundJob.Enqueue<WelcomeEmailJob>` (Hangfire); `WelcomeEmailJob.SendAsync` dùng `IEmailSender` (MailKit, SMTP; dev trỏ về MailHog) gửi email chào mừng có tên người dùng.

- **Vì sao:** enqueue thay vì gửi email đồng bộ ngay trong handler đăng ký — nếu SMTP chậm hoặc lỗi, người dùng vẫn đăng ký thành công ngay lập tức, việc gửi email được Hangfire tự retry (tối đa 3 lần, exponential backoff) ở nền.

- **Xong khi:** đăng ký tài khoản mới xong, mở MailHog thấy email chào mừng đến đúng địa chỉ trong vài giây.

- **Commit:** `feat(jobs): implement FR-JOB-001 welcome email enqueued after registration`

- **Bổ sung (OBS-001):** thêm check MinIO vào `AddHealthChecks()` (hiện mới có Postgres + Redis), gắn tag `ready` — tắt MinIO thì `/health/ready` trả 503.

**Nguyễn Ngọc Bảo Thịnh · FR-FILE-002 Xóa file khỏi MinIO + bổ sung log ra file** — ⬜ Chưa làm

- **Cách làm:** `DeleteFileCommand`/`IFileStorageService.DeleteAsync` xóa object theo key; gọi từ 3 nơi: khi ảnh bị gỡ khỏi Recipe/Category (FR-RCP-008), và khi `PurgeDeletedRecipesJob` (MT-01, Buổi 5) chạy dọn recipe quá hạn.

- **Vì sao:** gom logic xóa file vào một service duy nhất (`IFileStorageService`) để không có chỗ nào gọi thẳng SDK MinIO riêng lẻ — dễ audit và dễ đổi provider lưu trữ sau này nếu cần.

- **Xong khi:** gỡ ảnh khỏi recipe thì file cũng biến mất khỏi bucket; chạy thử `PurgeDeletedRecipesJob` xóa đúng ảnh của các recipe đã hết hạn 30 ngày.

- **Commit:** `feat(files): implement FR-FILE-002 delete wired into recipe cleanup and purge job`

- **Bổ sung (OBS-002):** thêm Serilog sink File (rolling theo ngày) bên cạnh Console + Seq đã có.

**Hồ Quốc Tiến · FR-JOB-002 Sinh Thumbnail + migration bảng `UploadedFiles`** — ⬜ Chưa làm

- **Cách làm (JOB-002):** `GenerateThumbnailJob` (Hangfire) được enqueue sau khi upload ảnh, dùng `IImageProcessor.Resize` sinh bản medium và thumbnail, lưu lại `MediumUrl`/`ThumbnailUrl`.

- **Cách làm (FILE-001 bổ sung):** thay khối `CREATE TABLE IF NOT EXISTS "UploadedFiles"` lúc khởi động trong `Program.cs` bằng EF migration `AddUploadedFiles`.

- **Vì sao:** resize ảnh tốn CPU nên chạy nền thay vì chặn request upload; bảng tạo bằng SQL lúc khởi động chỉ là giải pháp tạm khi gộp nhánh Tuần 4.

- **Xong khi:** upload ảnh xong vài giây sau có `MediumUrl`/`ThumbnailUrl`; `dotnet ef database update` tạo đúng bảng `UploadedFiles`.

- **Commit:** `feat(jobs): implement FR-JOB-002 thumbnail generation`

**Kiểm chứng cuối Buổi 7:** xóa ảnh hoạt động đúng qua MinIO thật; đăng ký tài khoản mới trên giao diện có email chào mừng trong MailHog và có dòng trong `AspNetUsers`; toàn bộ 34 FR đã rà lại trạng thái.

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
