# Kịch bản demo Lab 8 — Auth, Recipe, ảnh và Admin

**Sinh viên:** Chung Thiện Ý — **MSSV:** 2312804 — **Nhóm:** 18

**Ngày cập nhật:** 03/11/2026

## 1. Chuẩn bị

Cần Docker Desktop đang chạy, .NET SDK 10, Node.js 20 trở lên và dependencies đã cài. Script cần có sẵn các image `postgres:16-alpine`, `redis:7-alpine` và một MinIO tương thích. Script không tự pull image, không dùng volume của nhóm/LMS.

Image MinIO mặc định của công cụ test là `culinaryblog-minio-local:2025-10-15`, tận dụng image còn trên máy. Khi dùng máy khác, truyền tên image MinIO đã có bằng `-MinioImage 'ten-image:tag'`; không cần tạo lại thư mục local-minio đã xóa. Image phải có binary `/usr/local/bin/minio`.

Chạy kiểm thử tích hợp tại thư mục gốc:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\test-chung-thien-y-tuan-8.ps1
```

Kết quả đã kiểm chứng: **20/20 ca đạt**, gồm 12 Auth+Recipe và 8 Lab8. Script tạo PostgreSQL/Redis/MinIO tạm trên cổng ngẫu nhiên rồi tự dọn. Kết quả TRX được lưu ở `TestResults/week8`.

## 2. Mở API demo riêng

Terminal thứ nhất, tại thư mục gốc:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\test-chung-thien-y-tuan-8.ps1 -Demo
```

Đợi in đường dẫn `Demo manifest`. Script chuẩn bị database mới, tài khoản Owner/Other Author/Admin, thêm tài khoản Author để kiểm tra phân trang, một danh mục và bản nháp mẫu. Host dùng mã ứng dụng thật và ba dịch vụ thật, nhưng không chạy toàn bộ Program.cs, seeder lớn, SMTP hay recurring jobs. Worker chỉ xử lý queue dọn ảnh `recipe-images`.

Giữ terminal này mở. Kết thúc bằng Enter để host dừng và script dọn container. Phiên tự hết hạn sau tối đa 30 phút. Dữ liệu và tài khoản dùng thử không tồn tại trong database nhóm.

## 3. Chạy frontend mà không tạo tệp cấu hình local

Terminal thứ hai, tại thư mục gốc; thay đường dẫn manifest bằng đường dẫn vừa được in:

```powershell
$demoInfo = Get-Content -LiteralPath 'C:\...\culinary-week8-demo-...\demo.json' -Raw -Encoding utf8 | ConvertFrom-Json
$env:AUTH_DEMO_ENABLED = 'false'
$env:API_INTERNAL_BASE_URL = $demoInfo.apiBaseUrl
$env:NEXT_PUBLIC_API_BASE_URL = $demoInfo.apiBaseUrl
$env:AUTH_SECRET = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$env:AUTH_URL = 'http://localhost:3000'
$env:NEXTAUTH_URL = 'http://localhost:3000'
$env:AUTH_GOOGLE_ID = ''
$env:AUTH_GOOGLE_SECRET = ''
Set-Location .\frontend
node node_modules/next/dist/bin/next dev --hostname 127.0.0.1 --port 3000
```

Mở `http://localhost:3000`. Dùng CLI Next trực tiếp vì `npm run dev/build` có pre-script tự tạo `.env.local`. Những biến trên chỉ tồn tại trong terminal, không khôi phục các tệp local đã gỡ. Google bị tắt cho phiên trình diễn này; đăng nhập email dùng backend thật.

Nếu cần kiểm tra bản Production trước demo, thay lệnh cuối bằng:

```powershell
node node_modules/next/dist/bin/next build
node node_modules/next/dist/bin/next start --hostname 127.0.0.1 --port 3000
```

`NEXT_PUBLIC_API_BASE_URL` được đóng vào bundle khi build; mỗi phiên API demo có cổng mới nên cần build lại cho đúng phiên. Sau demo, đóng terminal, dừng frontend và build lại bằng biến môi trường của ứng dụng chạy chính khi cần. Không dùng secret tạm này cho triển khai.

## 4. Kịch bản chính, khoảng 8–10 phút

| Bước | Thao tác | Kết quả và nội dung giải thích |
| --- | --- | --- |
| 1 | Đăng nhập bằng `ownerEmail` và `password` trong manifest. | JWT thật của tài khoản Author được Identity tạo trong PostgreSQL. |
| 2 | Vào `/dashboard/recipes/new`; nhập tên Phở bò trình diễn Lab8, danh mục Món nước tuần 8, mô tả, chuẩn bị 20 phút, nấu 60 phút, 4 phần, Dễ. | Validate dữ liệu trước khi sang bước sau. |
| 3 | Thêm Thịt bò — 0,5 kg; bước Ninh nước dùng với mô tả. Chọn PNG/JPEG thật dưới 5 MB ở phần xem lại rồi lưu. | Tạo Draft trước, upload ảnh sau; chuyển sang trang sửa. Nếu upload lỗi có thể thử lại trên cùng Draft. |
| 4 | Tải thêm ảnh, nhập mô tả, đặt ảnh thứ hai làm đại diện; thử một tệp văn bản đổi đuôi PNG. | Ảnh thật hiện từ MinIO; PATCH đổi đại diện; ảnh giả bị từ chối, có thông báo lỗi. |
| 5 | Đổi tiêu đề và lưu thay đổi. | PUT có If-Match/RowVersion; tránh ghi đè dữ liệu phiên bản cũ. |
| 6 | Chọn Xuất bản, mở URL công khai trong cửa sổ khách. | Published, ảnh đại diện và nguyên liệu/bước hiển thị. Kiểm tra cả màn hình điện thoại. |
| 7 | Lưu trữ công thức, tải lại cửa sổ khách. Sau đó khôi phục/xuất bản lại. | Khách không xem nội dung Archived; công thức xuất bản lại xem được. |
| 8 | Xóa và xác nhận, tải lại URL công khai. | API xóa trả 204; đọc lại trả 404; không giữ nội dung Recipe cũ. Database dùng soft delete. |
| 9 | Dùng tài khoản Author mở `/dashboard/users`, sau đó đăng xuất và đăng nhập `adminEmail`. | Author bị chặn; Admin thấy mục Người dùng và danh sách. |
| 10 | Tìm tên/email, lọc Author/Admin, chuyển trang, xem ở mobile. | Lọc trước phân trang; không trả thông tin mật khẩu/token. |
| 11 | Mở một dòng JSONL trong thư mục `logDirectory` từ manifest. | Chỉ ra method/path/status/elapsed/UserId/CorrelationId/TraceId; không đưa token hoặc body đăng nhập lên màn hình. |

Để kiểm tra job xóa object, xóa riêng một ảnh trên trang sửa và quan sát log job/MinIO. Xóa Recipe là soft delete, không dùng thao tác đó để minh họa ảnh bị xóa vật lý ngay.

## 5. Những điểm cần giải thích khi được hỏi

- **Quyền sở hữu:** Author chỉ quản lý Recipe thuộc tài khoản; Admin có quyền mở rộng. Backend kiểm tra độc lập với giao diện.
- **Upload an toàn:** giới hạn dung lượng, đối chiếu đuôi/MIME/chữ ký, tên UUID, không gửi khóa MinIO ra frontend. Kiểm tra đầu tệp chưa phải giải mã toàn bộ ảnh.
- **Transaction và job:** đổi ảnh đại diện thực hiện trong transaction PostgreSQL; xóa object chạy sau commit để không làm mất ảnh khi metadata chưa lưu.
- **Concurrency:** If-Match cũ trả 409. Dùng hai cửa sổ để minh họa nhưng lưu ý TanStack Query có thể refetch khi đổi cửa sổ; bộ test HTTP đã kiểm tra trường hợp phiên bản cũ rõ ràng.
- **Cache:** trang chi tiết đọc no-store, Redis vẫn dùng ở backend. Tăng request để đổi lấy phản ánh trạng thái mới sau Archive/xóa.
- **Logging:** tệp JSONL theo ngày giữ 30 tệp; Seq chỉ cấu hình Development; tra sự cố bằng CorrelationId. Không ghi password/JWT/token reset.
- **Auth:** refresh token được rotate/thu hồi. Logout không blacklist tức thời JWT đã phát; token hiện tại vẫn hợp lệ đến hạn nếu giữ riêng bên ngoài trình duyệt.

## 6. Kiểm thử trình duyệt tự động

Script `frontend/scripts/test-week8-auth-recipe-ui.mjs` dùng Microsoft Edge và `playwright-core` đã cài ngoài project, không thêm dependency ứng dụng. Chạy sau khi mở API demo và build frontend Production bằng môi trường của chính phiên đó:

```powershell
$env:CULINARY_PLAYWRIGHT_MODULE_PATH = 'C:\...\node_modules\playwright-core\index.mjs'
$env:CULINARY_WEEK8_UI_MODE = 'production'
Set-Location .\frontend
node scripts/test-week8-auth-recipe-ui.mjs 'C:\...\culinary-week8-demo-...\demo.json'
```

Để cổng 3000 trống; script tự mở/dừng frontend của lần chạy. Kịch bản không mock API, chụp ảnh vào `spec/minh_chung_lab8_2026_11_03`. Kết quả đã chạy đạt gồm upload, vòng đời Recipe, phân quyền và trang Admin. Không dùng kết quả này để khẳng định Google OAuth, SMTP hoặc toàn hệ thống đã được kiểm thử.

## 7. Kết thúc

Dừng frontend bằng Ctrl+C; kết thúc terminal API demo bằng Enter. Kiểm tra không còn container tên `culinary-week8-*` của lần chạy. Không dọn image/volume nhóm hoặc LMS. Chi tiết kết quả, giới hạn và báo cáo Word được dẫn tại [báo cáo Lab8](bao_cao_ChungThienY_tuan_8.md).
