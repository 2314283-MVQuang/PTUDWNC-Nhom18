# Cấu hình đăng nhập Google cho website chung

**Phạm vi:** FR-AUTH-003 của Chung Thiện Ý, nhánh `2312804_ChungThienY_nhom18`.

## 1. Cách chia sẻ ứng dụng mà không đưa khóa lên GitHub

Người quản trị triển khai một website có domain HTTPS. Google Client Secret được cấp cho máy chủ chạy Next.js; người dùng chỉ truy cập website và chọn Đăng nhập với Google. Không cần gửi khóa cho từng người dùng. Push mã lên GitHub không tự triển khai website hoặc tự cấp OAuth Client.

| Thành phần | Cấu hình cần có | Được đưa lên GitHub? |
| --- | --- | --- |
| Mã nguồn, Dockerfile, Compose, hướng dẫn và `.env.prod.example` | Tên biến và giá trị mẫu | Có |
| `.env.prod` trên máy chủ | Khóa Google, JWT, Auth.js, SMTP và mật khẩu hạ tầng thật | Không |
| Server Next.js | `AUTH_GOOGLE_ID`, `AUTH_GOOGLE_SECRET`, `AUTH_SECRET`, `AUTH_URL` | Cấp lúc chạy, không nhúng vào image |
| API .NET | `Google__ClientId` khớp Client ID của Next.js | Cấp lúc chạy; API không cần Google Client Secret |
| Trình duyệt | URL website, trạng thái bật nút Google; OAuth Client ID có thể xuất hiện trong yêu cầu OAuth | Không nhận Client Secret |

Client ID là định danh ứng dụng, không phải mật khẩu. Client Secret phải được giữ trên server. Những người có quyền quản trị máy chủ/container có thể đọc cấu hình chạy; biến môi trường không phải cơ chế giấu khóa khỏi chính người quản trị.

Client ID được cung cấp đã điền trong `.env.prod.example`: `646582416171-fsbi8ueb444il6haggcdfino6j9f2904.apps.googleusercontent.com`. Giá trị không có dấu chấm cuối câu. Việc điền mẫu chưa thay đổi cấu hình máy chủ đang chạy. Trước khi triển khai, cần xác nhận client thuộc môi trường Production phù hợp và đã khai báo callback của domain chung; nếu client này chỉ dành cho Development thì dùng Client ID Production riêng.

## 2. Chuẩn bị Google Cloud Console

1. Dùng project/client Production riêng với môi trường thử nghiệm, loại **Web application**. Quy trình này không yêu cầu thay OAuth Client Development mà nhóm đang dùng trên localhost.
2. Hoàn thiện tên ứng dụng, email hỗ trợ và thông tin domain/trang chủ/chính sách riêng tư theo yêu cầu Google Console.
3. Để người ngoài tổ chức dùng website, chọn đối tượng phù hợp **External**. Khi sẵn sàng phục vụ công khai, kiểm tra trạng thái xuất bản của ứng dụng thay vì chỉ giữ một danh sách Test users. Tài khoản Workspace vẫn có thể bị chính sách tổ chức hạn chế.
4. Chỉ yêu cầu phạm vi đăng nhập cơ bản `openid`, `email`, `profile`. Quy trình xác minh bổ sung phụ thuộc phạm vi và yêu cầu Google hiển thị cho project; không mặc định mọi ứng dụng đều cần cùng một bước xác minh.
5. Với website ví dụ `https://blog.example.com`, thêm chính xác vào **Authorized redirect URIs**:

   ```text
   https://blog.example.com/api/auth/callback/google
   ```

   Nếu Console yêu cầu Authorized JavaScript origins, dùng origin `https://blog.example.com`, không thêm đường dẫn callback ở trường origins. Callback nằm ở frontend Next.js, không phải cổng API .NET. Không có khoảng trắng thừa và không thay domain thật bằng domain ví dụ.

6. Lưu Client ID/Client Secret vào cấu hình riêng của máy chủ. Không đưa tệp JSON tải từ Google vào repository hoặc Docker image. Khi cần đổi secret, cập nhật cấu hình máy chủ và tạo lại container frontend.

Hướng dẫn chính thức: [Google OAuth cho web server](https://developers.google.com/identity/protocols/oauth2/web-server), [chuẩn bị OAuth Production](https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance).

## 3. Cấp cấu hình trên máy chủ

Tại thư mục gốc dự án, sao chép mẫu một lần:

```bash
cp .env.prod.example .env.prod
```

PowerShell tương ứng:

```powershell
Copy-Item .env.prod.example .env.prod
```

Chỉ sao chép khi chưa có `.env.prod`, tránh ghi đè cấu hình đang sử dụng. Sửa trực tiếp trên máy chủ:

```dotenv
PUBLIC_WEB_URL=https://blog.example.com
PUBLIC_API_URL=https://api.example.com/api/v1
PUBLIC_STORAGE_URL=https://media.example.com
GOOGLE_CLIENT_ID=<Client ID của OAuth Web Client Production>
GOOGLE_CLIENT_SECRET='<Client Secret thật, chỉ lưu trên máy chủ>'
```

Những dòng trên chỉ minh họa vị trí cấu hình; thay cả dấu `<...>` bằng giá trị thật. Nếu secret chứa `$` hoặc ký tự đặc biệt, dùng dấu nháy đơn theo cú pháp dotenv của Docker Compose. Cấp đầy đủ các biến JWT/Auth.js, database, SMTP và Admin trong mẫu. Khóa Auth.js/JWT phải ổn định giữa các lần tạo lại container; không sinh lại mỗi lần cập nhật mã.

Compose Production đã lấy một `GOOGLE_CLIENT_ID` chuyển vào cả `api.Google__ClientId` và `frontend.AUTH_GOOGLE_ID`. Secret chỉ chuyển vào `frontend.AUTH_GOOGLE_SECRET`. Không cần sửa `appsettings.json` hoặc ghi Client ID cá nhân vào mã. Khi chưa dùng Google, có thể để trống cả hai biến; khi chỉ điền một biến, nút Google không bật.

Tệp `.env.prod` không được Git theo dõi và bị loại khỏi Docker build context. `.env.local` dành cho Development; website chung dùng cấu hình Production trên máy chủ. Không dùng prefix `NEXT_PUBLIC_` cho Client Secret và không truyền secret bằng Docker `--build-arg`.

## 4. Kiểm tra cấu hình trước khi chạy

```bash
node scripts/check-google-config.mjs
```

Có thể truyền đường dẫn tệp môi trường đã chuẩn bị:

```bash
node scripts/check-google-config.mjs /path/to/server.env
```

Lệnh gọi Docker Compose để đọc cấu hình đã resolve, kiểm tra đủ cặp Google, Client ID hai phía, secret không nằm trong biến public/API và origin HTTPS. Kết quả chỉ in lỗi theo tên biến hoặc callback công khai; không in secret. Không chạy container, không đăng nhập Google và không chứng minh Client Secret hợp lệ hoặc domain đã truy cập được.

Không dùng `docker compose config` rồi chia sẻ nguyên output: cấu hình resolve có thể chứa các mật khẩu thật. Khi kiểm tra Git, `git check-ignore .env.prod frontend/.env.local` phải cho thấy các tệp bí mật bị bỏ qua. `.gitignore` không bảo vệ một secret đã commit trong quá khứ; nếu từng lộ, cần thu hồi/đổi khóa và xử lý lịch sử riêng.

## 5. Khởi động hoặc cập nhật website

Sau khi có DNS, HTTPS/reverse proxy và cấu hình đầy đủ, người phụ trách triển khai chạy:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
```

Thao tác này thuộc giai đoạn triển khai, chưa được thực hiện trong đợt chuẩn hóa cấu hình. Để cập nhật về sau: lấy phiên bản mã đã được duyệt, giữ nguyên `.env.prod`, kiểm tra cấu hình rồi chạy lại lệnh trên. Nếu chỉ đổi Client Secret, dùng:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --no-deps --force-recreate frontend
```

Nếu đổi Client ID, tạo lại cả API và frontend. `docker compose restart` không nạp giá trị môi trường mới. Không chạy lệnh xóa volume để cập nhật Google. Nếu đổi `PUBLIC_API_URL`, build lại frontend vì URL `NEXT_PUBLIC_API_BASE_URL` được nhúng lúc build.

Dockerfile frontend dùng Next CLI trực tiếp, không chạy prebuild tạo `.env.local`. Một image được build không có khóa Google vẫn bật được Google khi cặp khóa được cấp lúc khởi động. [Docker khuyến cáo không truyền secret qua build ARG/ENV](https://docs.docker.com/build/building/variables/).

## 6. Khi dùng GitHub Actions hoặc dịch vụ hosting

Có thể giữ secret ở phần quản lý environment của dịch vụ hosting hoặc **GitHub Actions Secrets** nếu workflow triển khai có bước cấp chúng cho máy chủ. Không đặt secret trong mã YAML hay commit tệp `.env.prod` được tạo từ secret.

Workflow `.github/workflows/ci.yml` hiện tại chỉ build/test .NET và publish image API theo điều kiện có sẵn; chưa triển khai website/frontend và chưa truyền Google secret đến máy chủ. Vì vậy chỉ thêm GitHub Secret chưa làm Google hoạt động. Không sửa workflow của nhóm hoặc triển khai máy chủ trong phạm vi đợt này. [Tài liệu GitHub Secrets](https://docs.github.com/en/actions/concepts/security/secrets).

Người clone mã về máy riêng không tự nhận được secret từ GitHub. Thành viên cần môi trường thử nghiệm riêng hoặc được cấp cấu hình ngoài Git theo quyền được giao; người dùng website chung không cần làm bước này.

## 7. Kiểm chứng sau khi người quản trị cấp cấu hình thật

1. Mở `https://<domain>/api/auth/providers`: có provider `google`; phản hồi không chứa Client Secret.
2. Mở trang `/login`: thấy nút Đăng nhập với Google.
3. Nhấn nút, kiểm tra Google hiển thị đúng tên ứng dụng/domain và callback không báo `redirect_uri_mismatch`.
4. Hoàn tất đăng nhập bằng tài khoản được phép. Backend xác minh Google ID token, trả JWT ứng dụng; trình duyệt hiển thị tài khoản đúng.
5. Thử một tài khoản Google khác được phép để kiểm tra phạm vi người dùng chung, đăng xuất và kiểm tra lại phiên.

| Hiện tượng | Điểm cần kiểm tra |
| --- | --- |
| Không có nút Google | Cặp biến `AUTH_GOOGLE_*` của container frontend, khởi động lại đúng cách bằng tạo lại container |
| Nút hiện nhưng Google từ chối client | Client Secret tương ứng đúng Client ID, trạng thái OAuth Client |
| `redirect_uri_mismatch` | URI trong Console phải khớp scheme/domain/port/path với callback của website |
| Google đồng ý nhưng ứng dụng chưa đăng nhập | Client ID frontend/API, API có hoạt động, log xác thực không chứa token |
| Chỉ một số tài khoản dùng được | Audience, trạng thái xuất bản/Test users, chính sách tổ chức Google Workspace |

Kiểm thử cấu hình bằng khóa giả có thể xác nhận provider/nút và việc không lộ secret trong phản hồi; không thay thế việc đăng nhập thật. Đợt này không có Client Secret Production/domain thực nên chưa xác nhận toàn bộ luồng Google trên website công khai.

## 8. Kết quả chuẩn hóa ngày 03/11/2026

- Dùng chung một hàm đọc cấu hình cho Auth.js và trang đăng nhập/đăng ký; chỉ truyền cờ bật/tắt xuống giao diện.
- Bỏ Client ID cá nhân trong `appsettings.json`. API chưa được cấp Client ID sẽ từ chối đăng nhập Google có thông báo rõ ràng; đăng nhập mật khẩu không bị bật/tắt theo cấu hình Google.
- Bổ sung ignore cho các tệp môi trường thật và JSON credentials; Dockerfile build trực tiếp bằng Next CLI để không tạo `.env.local` chứa khóa trong image.
- Công cụ kiểm tra Compose đã chạy bằng tệp cấu hình tạm chứa khóa giả: cặp hợp lệ được chấp nhận, thiếu Secret bị từ chối; output không chứa giá trị khóa.
- Frontend unit: **36/36 đạt**; backend hồi quy: **204 đạt, 32 bỏ qua theo điều kiện môi trường, 0 lỗi**. Các ca bỏ qua không được tính là đạt trong lần này.
- Runtime Production: ba chế độ không cấu hình, thiếu Secret, đủ cặp khóa giả đều có provider/nút khớp nhau; Secret giả không xuất hiện trong HTML, phản hồi providers/session hoặc log thu thập. Kết quả ở [runtime.json](minh_chung_google_2026_11_03/runtime.json).
- Chưa triển khai máy chủ, chưa thay Google Console, chưa dùng Client Secret thật và chưa khẳng định người dùng Google ngoài nhóm đã đăng nhập được.
