# Báo cáo Lab 8 — Giao diện, upload ảnh và kiểm thử tích hợp

**Môn học:** Phát triển ứng dụng web nâng cao

**Sinh viên:** Chung Thiện Ý — **MSSV:** 2312804 — **Nhóm:** 18

**Thời gian báo cáo:** 27/10/2026–03/11/2026

**Ngày kiểm chứng bổ sung:** 03/11/2026

## 1. Phạm vi thực hiện

Theo bảng phân công, nhiệm vụ tuần 8 của Chung Thiện Ý gồm kiểm thử luồng Auth kết hợp Recipe và chuẩn bị demo đăng nhập → tạo/xuất bản công thức. Ngày 28/10 đã hoàn thành 12 ca tích hợp và sửa cache trang chi tiết sau khi xóa. Minh chứng của giai đoạn đó được giữ ở [minh_chung_tuan_8](minh_chung_tuan_8/).

Theo xác nhận mở rộng phạm vi Lab 8, phần bổ sung gồm chỉnh giao diện Recipe/dashboard, upload ảnh MinIO theo SRS, hoàn thiện thao tác vòng đời Recipe, trang danh sách người dùng dành cho Admin và logging. Phần mở rộng có sử dụng hạ tầng dùng chung và các API đã có của thành viên khác. Những API được kế thừa được ghi nhận là tích hợp và kiểm thử, không tính toàn bộ thành mã mới của cá nhân.

| Yêu cầu tối thiểu Lab 8 | Kết quả trong phạm vi đã xác nhận |
| --- | --- |
| Upload ảnh | API multipart, kiểm tra tệp, lưu MinIO, giao diện upload/chọn ảnh đại diện/xóa ảnh. |
| Thêm, cập nhật và thay đổi trạng thái công thức | Wizard tạo, biểu mẫu sửa, Publish/Archive/khôi phục/xóa mềm, lọc danh sách theo chủ sở hữu và trạng thái. |
| Danh sách người dùng Admin/tác giả | API và trang chỉ dành cho Admin; tìm kiếm tên/email, lọc vai trò, phân trang. |
| Logging | HTTP log có ngữ cảnh, Console JSON, tệp JSONL theo ngày, Seq trong Development. |
| Đối chiếu SRS | Phần ảnh, quyền Recipe, concurrency, cache, soft delete và logging liên quan đến phạm vi trên. |

## 2. Điều chỉnh giao diện frontend

Trang chi tiết trước đó có vùng tiêu đề chiếm nhiều chiều cao, dải thông số thời gian nằm sát vùng chuyển tiếp và khung dinh dưỡng rỗng. Nội dung ngắn khiến trang có khoảng trống lớn trước footer. Phần điều chỉnh giữ tông cam hiện có, thu gọn vùng tiêu đề, đặt dải thông số trong luồng bố cục bình thường và giảm khoảng cách chân trang.

Trên màn hình lớn, các bước thực hiện nằm ở cột chính, nguyên liệu và dinh dưỡng nằm ở cột bên. Trên điện thoại, nội dung chuyển thành một cột và thông số thành lưới hai cột. Khung dinh dưỡng chỉ xuất hiện khi có dữ liệu. Bố cục dashboard dùng cột nội dung có khả năng co lại để bảng người dùng không kéo tràn toàn trang.

Trang chi tiết tiếp tục lấy Recipe với `cache: "no-store"`. Sau Archive hoặc xóa, request tiếp theo phản ánh trạng thái mới thay vì hiển thị nội dung cũ. API trả 403 khi công thức không công khai được chuyển thành trang không tìm thấy đối với khách. Cache danh mục và Redis backend vẫn được sử dụng.

**Ưu điểm:** nội dung rõ hơn, trang ngắn gọn, hiển thị được trên điện thoại và trạng thái công khai cập nhật ngay. **Đánh đổi:** Next.js gửi thêm request đọc Recipe; việc tải ảnh kích thước gốc chưa được thay bằng pipeline thumbnail trong phạm vi này.

Minh chứng: [trang chi tiết desktop](minh_chung_lab8_2026_11_03/recipe-public.png), [mobile](minh_chung_lab8_2026_11_03/recipe-mobile.png), [trang sau xóa](minh_chung_lab8_2026_11_03/recipe-after-delete-fixed.png). Ảnh trong minh chứng được tạo để kiểm thử, không phải ảnh món ăn thật.

## 3. Upload ảnh theo đặc tả

### 3.1. Luồng xử lý

SRS trang 33–34 quy định POST ảnh nhận `multipart/form-data`. Endpoint hiện có chưa hoàn chỉnh luồng này nên đã được đồng bộ:

1. Frontend gửi trường `file` và `altText` tùy chọn đến POST `/api/v1/recipes/{id}/images` bằng FormData.
2. Endpoint kiểm tra kiểu request, sự có mặt và kích thước tệp. Không đặt thủ công Content-Type của FormData để trình duyệt tự tạo boundary.
3. Handler kiểm tra Recipe tồn tại và người gọi là chủ sở hữu hoặc Admin trước khi ghi object vào MinIO.
4. Kiểm tra phần mở rộng, MIME và chữ ký đầu tệp phải cùng khớp JPEG, PNG, WebP hoặc AVIF; dung lượng lớn hơn 0 và không vượt 5 MiB (5 × 1024 × 1024 byte). AltText tối đa 200 ký tự.
5. Lưu object với khóa `recipes/{recipeId}/{uuid}.{ext}`. Tên gốc do người dùng gửi không được dùng làm đường dẫn lưu trữ.
6. Lưu metadata qua repository/Unit of Work. Ảnh đầu tiên tự thành ảnh đại diện; các ảnh sau được xếp theo thứ tự.
7. Xóa các cache tag Recipe, danh sách, Search và Category có liên quan. Trả 201 cùng dữ liệu ảnh.

Phần AVIF đọc thương hiệu `ftyp`, không coi trường phiên bản nhỏ là thương hiệu ảnh hợp lệ. Việc kiểm tra đầu tệp giúp từ chối tệp giả/đổi đuôi thường gặp; chưa thay thế một bộ giải mã kiểm tra toàn bộ nội dung ảnh.

### 3.2. Xử lý thất bại và lưu trữ

Nếu MinIO không hoạt động, middleware trả Problem Details 503 với mã `FILE_STORAGE_UNAVAILABLE`. Nếu đã upload nhưng lưu metadata thất bại, handler cố gắng xóa object vừa tạo để hạn chế ảnh mồ côi. Bước xóa cache nằm sau lưu metadata; lỗi cache không được làm mất object của ảnh đã lưu thành công.

Adapter tạo bucket khi bucket chưa tồn tại và cấp quyền đọc công khai chỉ cho tiền tố `recipes/*`; không mở quyền ghi công khai. Với bucket đã có, policy được giữ nguyên. Nếu bucket hiện tại chưa cho phép đọc ảnh, người quản trị cần kiểm tra policy hoặc cấu hình URL phân phối ảnh; không tự ghi đè policy đang dùng của nhóm.

`MinIO:Endpoint` dùng để backend gọi dịch vụ, `MinIO:PublicEndpoint` dùng để tạo URL trình duyệt truy cập. Compose được bổ sung giá trị public `http://localhost:9000` cho môi trường phát triển; khi triển khai cần đặt URL phù hợp với máy chủ thực tế.

### 3.3. Giao diện quản lý ảnh

Wizard cho phép chọn ảnh ở bước xem lại. Bản nháp được tạo trước rồi mới upload ảnh vì đường dẫn object cần RecipeId. Nếu upload lỗi, giao diện giữ ID bản nháp để thử lại, tránh tạo thêm Recipe trùng.

Trang sửa hỗ trợ chọn tệp, mô tả ảnh, thông báo đang tải/lỗi/thành công, chọn ảnh đại diện và xóa ảnh. Thao tác đặt ảnh đại diện được đồng bộ sang PATCH `/images/{imageId}/primary`. Khi xóa ảnh đại diện, backend bỏ cờ đại diện cũ trước rồi chọn ảnh còn lại trong cùng transaction để tránh vi phạm unique index PostgreSQL.

Sau khi metadata ảnh được xóa mềm và commit, Hangfire nhận job dọn object MinIO ở queue `recipe-images`, có ba lần thử lại. Nếu enqueue lỗi, hệ thống ghi log; metadata đã xóa không bị phục hồi. Job chỉ xử lý đường dẫn thuộc đúng Recipe trong kho ảnh, không xóa URL ngoài hoặc dữ liệu thuộc công thức khác.

**Ưu điểm:** đồng bộ được ảnh thực tế với database, hỗ trợ thử lại khi lỗi và không chặn request xóa để chờ kho ảnh. **Đánh đổi:** quy trình liên quan hai hệ thống nên cần bù trừ/job; nếu job không được enqueue thành công thì cần theo dõi log để dọn ảnh còn sót.

Minh chứng: [quản lý ảnh](minh_chung_lab8_2026_11_03/recipe-images.png).

## 4. Tạo, sửa và thay đổi trạng thái Recipe

Các command tạo/sửa/Publish/Archive/khôi phục/xóa đã có được rà soát và kết nối vào giao diện. Phần bổ sung không thay đổi quy tắc nghiệp vụ đã thống nhất:

| Trạng thái/thao tác | Hành vi |
| --- | --- |
| Tạo mới | Lưu Draft, AuthorId lấy từ tài khoản xác thực. |
| Sửa | Gửi If-Match/RowVersion; phiên bản cũ nhận 409 thay vì ghi đè. |
| Publish | Cần ít nhất một nguyên liệu và một bước; sau thành công khách đọc được. |
| Archive | Ẩn khỏi nội dung công khai, chủ sở hữu vẫn quản lý được. |
| Khôi phục | Dùng API hiện có để đưa công thức về trạng thái phù hợp và xuất bản lại. |
| Xóa | Xóa mềm bằng IsDeleted/DeletedAt theo quyết định SRS của nhóm. |

Trang `/dashboard/recipes` được thay bằng workspace có tải/lỗi/rỗng, phân trang và bộ lọc trạng thái. Tham số `mine=true` lọc AuthorId tại server, trước Count/Skip/Take. Admin có lựa chọn toàn bộ hoặc công thức thuộc tài khoản hiện tại. Việc lọc ở server tránh bỏ sót dữ liệu khi trang hiện tại chỉ chứa một phần danh sách.

Biểu mẫu sửa kiểm tra chủ sở hữu/Admin trước khi cho thao tác. Backend vẫn kiểm tra quyền độc lập; ẩn nút trên frontend không phải biện pháp bảo vệ duy nhất. Giữ nguyên Instructions tùy chọn, xử lý slug và điều kiện Publish đã được thống nhất.

Minh chứng: [Draft](minh_chung_lab8_2026_11_03/recipe-draft.png), [sau Publish](minh_chung_lab8_2026_11_03/recipe-published.png).

## 5. Trang danh sách người dùng

GET `/api/v1/users` và trang `/dashboard/users` chỉ dành cho Admin. Danh sách gồm tài khoản Admin/Author với tên, email, avatar, vai trò, trạng thái hoạt động và ngày tạo. Author không được truy cập danh sách tài khoản của hệ thống.

API nhận `page`, `pageSize`, `role` và `q`. PageSize tối đa 50; giao diện dùng 12. Bộ lọc vai trò chấp nhận Admin/Author, ô tìm kiếm áp dụng tên/email. Truy vấn lọc trước khi phân trang, sắp xếp theo ngày tạo rồi ID để kết quả ổn định. Role của người dùng trong trang được đọc theo một tập ID, tránh truy vấn từng tài khoản.

DTO chỉ chứa dữ liệu hiển thị, không có PasswordHash, SecurityStamp, token hoặc thông tin xác thực nội bộ. Phản hồi HTTP có `Cache-Control: no-store`; query key phía frontend phân biệt tài khoản, trang và điều kiện lọc. Menu chỉ hiện cho Admin, middleware chặn đường dẫn và handler kiểm tra quyền lần nữa.

**Ưu điểm:** đủ chức năng tra cứu, giới hạn dữ liệu trả về và có kiểm soát quyền tại server. **Giới hạn:** đây là danh sách chỉ đọc; chưa bổ sung khóa tài khoản, sửa vai trò hoặc xóa người dùng.

Minh chứng: [desktop](minh_chung_lab8_2026_11_03/admin-users.png), [mobile](minh_chung_lab8_2026_11_03/admin-users-mobile.png).

## 6. Logging hệ thống

Theo phần observability tại SRS trang 39, mỗi request có log gồm method, path, status, elapsed time, UserId, CorrelationId và TraceId. Request lỗi hoặc xử lý trên 500 ms được ghi mức cảnh báo/lỗi phù hợp. Pipeline MediatR tiếp tục ghi loại Command/Query và thời gian, bổ sung trường hợp thất bại.

Middleware CorrelationId nhận một giá trị tối đa 64 ký tự, chỉ cho chữ ASCII, số, dấu gạch ngang, gạch dưới và dấu chấm; dữ liệu không hợp lệ được thay bằng GUID. Cách này giữ được khả năng tra cứu xuyên request mà không ghi header tùy ý vào log.

Cấu hình chung ghi Console JSON và tệp `logs/culinary-YYYYMMDD.jsonl`, xoay theo ngày, giữ tối đa 30 tệp. Đường dẫn tương đối theo thư mục làm việc của API. Development bổ sung Seq tại cấu hình hiện có. Production không phụ thuộc Seq mặc định. Thư mục log được loại khỏi Git.

Không ghi body đăng nhập, password, Authorization header, JWT, refresh token hoặc token xác nhận/đặt lại mật khẩu. Dịch vụ email mô phỏng không còn ghi token vào Console. Log request không ghi query string hay body; UserId và đường dẫn vẫn là dữ liệu vận hành cần được quản lý quyền truy cập khi triển khai.

**Ưu điểm:** tra được request lỗi theo CorrelationId, tìm được API chậm và có tệp lưu lại khi không mở Seq. **Đánh đổi:** tăng I/O và dung lượng lưu; cần theo dõi thời gian giữ log và quyền đọc thư mục trên máy triển khai.

Minh chứng: [một sự kiện HTTP JSON thực tế](minh_chung_lab8_2026_11_03/http-log-sample.json). Bộ kiểm thử cũng đọc tệp log thực để xác nhận trường dữ liệu và sự vắng mặt của các giá trị bí mật dùng trong test.

## 7. Cấu trúc mã nguồn chính

| Nhóm | Tệp tiêu biểu và trách nhiệm |
| --- | --- |
| Upload API/Application | `RecipeItemEndpoints.cs`, `UploadRecipeImageCommand.cs`: nhận multipart, quyền sở hữu, kiểm tra tệp và lưu metadata. |
| Kho ảnh | `IRecipeImageStorage.cs`, `MinioRecipeImageStorage.cs`: tách nghiệp vụ khỏi SDK MinIO. |
| Dọn ảnh | `IRecipeImageCleanup.cs`, `RecipeImageCleanupJob.cs`, `DeleteRecipeImageCommandHandler.cs`: commit metadata rồi enqueue dọn object. |
| Người dùng | `UsersEndpoints.cs`, `GetUsersQuery.cs`, `UserDirectory.cs`, `types/admin.ts`, `dashboard/users/page.tsx`. |
| Recipe UI | `RecipeWizardForm.tsx`, `RecipeEditForm.tsx`, `ImagesEditor.tsx`, `RecipeWorkspace.tsx`, trang chi tiết. |
| Logging | `SystemLoggingExtensions.cs`, `CorrelationIdMiddleware.cs`, `LoggingBehavior.cs`, `appsettings*.json`. |
| Kiểm thử | `AuthRecipeFlowTests.cs`, `Lab8ApiTests.cs`, `ImageFileValidationTests.cs`, `LoggingConfigurationTests.cs`. |
| Công cụ | `test-chung-thien-y-tuan-8.ps1`, `test-week8-auth-recipe-ui.mjs`, `scripts/build-lab8-report.py`. |

Kiến trúc tiếp tục giữ API → MediatR → Application → abstraction → Infrastructure. Chỉ Infrastructure làm việc trực tiếp với MinIO/Identity database; frontend không giữ khóa MinIO.

## 8. Kiểm thử và kết quả

### 8.1. Môi trường riêng

Script tạo ba container PostgreSQL 16, Redis 7 và MinIO với tên GUID, cổng loopback ngẫu nhiên, không gắn volume nhóm/LMS. Database có tiền tố riêng; fixture từ chối database ngoài quy tắc. SQL khởi tạo và patch Instructions nullable được áp dụng vào database mới để kiểm chứng cả trigger RowVersion thật.

Host kiểm thử dùng dịch vụ Auth/Recipe, Identity, JWT, repository, Unit of Work, Redis, middleware và endpoint ứng dụng. Đây là host test, không chạy toàn bộ Program.cs hay seeder/recurring jobs. Worker Hangfire chỉ chạy queue dọn ảnh, không gửi welcome email. Không tính enqueue email thành bằng chứng SMTP đã gửi thư.

### 8.2. Ma trận kiểm chứng

| Phạm vi | Kết quả ngày 03/11/2026 |
| --- | --- |
| Tích hợp Auth + Recipe | 12 ca đạt: đăng ký/login/refresh, AuthorId, quyền sở hữu/Admin, Publish thiếu dữ liệu, concurrency, soft delete/cache, token rotate/reuse/logout/hết hạn. |
| Tích hợp bổ sung Lab 8 | 8 ca đạt: upload MinIO/đọc bytes, đổi và xóa ảnh đại diện/job xóa object; tệp giả/sai MIME/quá dung lượng; từ chối tác giả khác; danh sách Admin/tìm kiếm/lọc/phân trang; mine/status/Archive; log JSON không chứa bí mật. |
| Tổng tích hợp | **20/20 đạt, 0 lỗi, 0 bỏ qua**. |
| Hồi quy mặc định | **144 đạt, 30 bỏ qua, 0 lỗi; tổng 174**. Ca bỏ qua cần môi trường riêng, không được tính là đạt trong lần này. |
| Frontend Production | Build, kiểm tra kiểu và lint trong build đạt. |
| Trình duyệt Edge, API thật | Upload ảnh hợp lệ; từ chối ảnh giả; đổi ảnh đại diện; tạo/sửa/Publish/Archive/khôi phục/xóa; trang công khai; quyền Author; danh sách Admin, tìm kiếm, lọc, phân trang; desktop/mobile đều đạt. Không ghi nhận lỗi JavaScript trong kịch bản. |

Kết quả máy đọc được tại [ket_qua_kiem_thu.json](minh_chung_lab8_2026_11_03/ket_qua_kiem_thu.json). TRX ở `TestResults/week8/auth-recipe-week8.trx` và `TestResults/week8/regression-week8.trx`; thư mục kết quả chạy không được đưa vào Git.

Trong lúc kiểm chứng đã sửa các vấn đề của fixture: chạy tuần tự các host dùng cùng database tạm; tái sử dụng danh mục seed khi phù hợp; override cả Endpoint/PublicEndpoint của MinIO; gắn request logger đúng logger của từng host. Script trình duyệt chờ phản hồi lọc người dùng trước khi kiểm tra hàng trong bảng, tránh đọc nhầm dữ liệu trang trước. Các chỉnh sửa này không nới lỏng điều kiện nghiệp vụ.

## 9. Cấu hình chung và dữ liệu được giữ

Theo yêu cầu trước đó, các cấu hình riêng trên máy đã được gỡ: `run-local.ps1`, `docker-compose.local.yml`, `local-minio/`, `frontend/.env.local` và User Secrets của API. Những container Culinary đang dùng override cũ đã được dừng. Image, volume, dữ liệu nhóm và LMS được giữ; Redis dùng cấu hình chung không bị dừng theo thao tác này.

Phần Lab 8 chỉ bổ sung cấu hình logging dùng chung, public endpoint MinIO trong Compose và package sink File tương ứng. Không tạo lại override/local secrets. Image MinIO trong Compose được trả về giá trị đã commit trên nhánh; chưa xác nhận image đó pull được từ registry. Bộ test dùng image có sẵn và có tham số `-MinioImage` để chọn image tương thích, không tự pull hay build lại image cá nhân.

Các cổng chung 5432/8080 từng bị ứng dụng khác chiếm; việc kiểm thử bằng cổng ngẫu nhiên không giải quyết thay xung đột của môi trường chạy chính. JWT/Google cần được cấp cấu hình hợp lệ khi chạy ứng dụng sau khi xóa secret. Không lưu lại secret mẫu dùng thử vào cấu hình nhóm.

## 10. Giới hạn và nội dung trình bày

Phạm vi Lab 8 đã xác nhận được hoàn thành và kiểm chứng như ma trận trên. Chưa nhận hoàn thành toàn bộ 34 FR hoặc toàn bộ phần việc của thành viên khác. Thumbnail pipeline, quản trị thay đổi vai trò/khóa tài khoản và triển khai máy chủ không thuộc phần bổ sung này. Google OAuth thực tế không được kiểm thử lại sau khi gỡ cấu hình riêng.

Logout hiện thu hồi refresh token và xóa phiên frontend; JWT đã phát vẫn hợp lệ đến hạn nếu được giữ bên ngoài trình duyệt. Công thức xóa mềm và URL ảnh lịch sử ngoài tiền tố quản lý không bị xóa vật lý bởi job mới. Đây là hành vi/giới hạn được ghi nhận, không trình bày thành bảo đảm khác.

Kịch bản trình bày theo [hướng dẫn demo](kich_ban_demo_ChungThienY_tuan_8.md). Báo cáo Word theo mẫu Lab4 nằm ở `spec/Lab8_2312804_ChungThienY.docx`; nội dung giải thích chi tiết và bằng chứng được liên kết trong bản Markdown này. Trạng thái đồng bộ GitHub được ghi nhận riêng ở phần dưới.

## 11. Kiểm tra khi đồng bộ nhánh GitHub

Phần Lab8 được lưu ở commit `f88555c`. Khi chuẩn bị push, nhánh GitHub đã có thêm các thay đổi đến `afeeaaf`, bao gồm xử lý ảnh, bảo vệ nội dung bản nháp, Search, thumbnail, dashboard và triển khai của nhóm. Những thay đổi này được tích hợp vào nhánh cá nhân, giữ lịch sử hai phía.

Các điểm xử lý khi ghép mã:

- Giữ giao diện và upload multipart JPEG/PNG/WebP/AVIF tối đa 5 MiB của Lab8. Endpoint cũng tiếp tục nhận JSON metadata để tương thích với helper presigned URL đã có của nhóm; giới hạn riêng của module File không được đổi theo giới hạn của form Lab8.
- Giữ dialog xác nhận xóa ảnh, định dạng số thập phân và sidebar có đánh dấu mục đang mở. Trang người dùng vẫn chỉ dành cho Admin; quyền trang danh mục giữ theo nhánh nhóm.
- Giữ kiểm tra quyền đọc bản nháp cho endpoint ảnh/nguyên liệu/bước. Kiểm thử đọc ảnh nháp được điều chỉnh để gửi token chủ sở hữu, không nới lỏng quyền backend.
- Giữ job xóa các bản gốc/medium/thumbnail của nhóm, cùng transaction chuyển ảnh đại diện và queue dọn ảnh mới. Hai job có thể cùng nhận một object; thao tác xóa object được thực hiện theo cơ chế idempotent của MinIO.
- Giữ `MinioBucketInitializer` từ nhánh nhóm. Service này áp dụng public-read cho bucket khi khởi động, khác với quyền chỉ tiền tố `recipes/*` trong adapter upload mới; cấu hình triển khai cần được đánh giá theo cả hai thành phần. Pipeline thumbnail hiện có của nhóm không được tính thành phần lập trình mới của Lab8 cá nhân.
- Bỏ dòng PackageReference Serilog.Sinks.File bị lặp do hai phía cùng thêm. Không nâng các dependency hiện có trong bước đồng bộ này.

Kiểm chứng sau tích hợp: **202 backend đạt, 32 bỏ qua, 0 lỗi**; **32/32 frontend unit đạt**; TypeScript, ESLint trong build và build Production đạt. Bộ tích hợp riêng vẫn **20/20 đạt**, bổ sung xác nhận JSON metadata tương thích và từ chối tác giả khác ở cả hai dạng upload. Kết quả tại [kiem_tra_sau_tich_hop.json](minh_chung_lab8_2026_11_03/kiem_tra_sau_tich_hop.json). Các ảnh trình duyệt phía trên phản ánh phiên kiểm tra Lab8 trước tích hợp; không tính thành lần chạy trình duyệt mới trên bản merge.

Tài liệu Word/PDF tiếp tục nằm ngoài commit theo quy tắc `.gitignore` hiện có của nhóm; bản Markdown, mã nguồn và ảnh minh chứng được đưa vào nhánh.
