# Culinary Blog – SRS bổ sung v1.1.0

> Tài liệu bổ sung cho **SRS_Culinary_Blog_v1.0.0.pdf**. Giữ nguyên mọi FR của v1.0.0, thêm 2 module:
>
> - **3.8. Module Bình luận Công thức (FR-CMT)**: 4 FR mới.
> - **3.9. Module Quản trị Người dùng (FR-USR)**: 2 FR mới.
>
> Tổng số FR tăng từ 27 lên 33. Người soạn: Mai Văn Quang (2314283), nhóm 18, Tuần 9.

## Lịch sử phiên bản

| Phiên bản | Ngày | Nội dung |
|---|---|---|
| 1.0.0 | — | Bản gốc: 27 FR, 7 module |
| 1.1.0 | 03/11/2026 | Thêm FR-CMT-001..004 (bình luận) và FR-USR-001..002 (quản trị người dùng) |

## Thay đổi ở các mục khác của SRS v1.0.0

| Mục | Thay đổi |
|---|---|
| 2.2. Chức năng sản phẩm | Thêm 2 nhóm: **Bình luận (FR-CMT, 4 FR)** và **Quản trị người dùng (FR-USR, 2 FR)** |
| 2.3. Lớp người dùng | **Khách**: xem bình luận. **Tác giả**: viết, sửa, xoá bình luận của mình; xoá bình luận dưới công thức của mình. **Admin**: xoá mọi bình luận, xem danh sách người dùng, khoá/mở khoá tài khoản |
| 7. Mô hình dữ liệu | Thêm bảng `RecipeComments` (mục D.1 bên dưới). Dùng cột có sẵn `AspNetUsers.IsActive` cho FR-USR-002 |
| 8. API | Thêm 6 endpoint (mục D.2 bên dưới) |
| 5.2. Bảo mật | Thêm rate limit `comments`: 5 bình luận mới/phút/người dùng. Nội dung bình luận luôn hiển thị dạng text (không render HTML, chống XSS) |

---

## 3.8. Module Bình luận Công thức (FR-CMT)

Người dùng đã đăng nhập trao đổi dưới mỗi công thức đã xuất bản: hỏi mẹo nấu, góp ý, cảm ơn. Bình luận phẳng (không trả lời lồng nhau), xoá mềm như mọi entity khác. Cột `CreatedAt`, `UpdatedAt` và `RowVersion` do AuditInterceptor và trigger `touch_row()` quản lý (mục 7.1).

### FR-CMT-001: Xem Danh sách Bình luận của Công thức

| Mục | Nội dung |
|---|---|
| Mã yêu cầu | FR-CMT-001 |
| Tên yêu cầu | Xem danh sách bình luận của công thức |
| Nhóm chức năng | Module Bình luận Công thức (FR-CMT) |
| Tác nhân | Khách, Tác giả, Admin |
| Mức ưu tiên | M – Must Have |
| Mô tả | Trả về bình luận chưa bị xoá của 1 công thức, **mới nhất trước**, có phân trang offset (mục 8). Mỗi bình luận kèm tên và avatar người viết, cùng 2 cờ `canEdit`/`canDelete` tính theo người đang xem để giao diện chỉ hiện nút được phép. |
| Điều kiện tiên quyết | Công thức tồn tại và người xem được phép xem nó (FR-RCP-002: Published thì ai cũng xem; Draft/Archived chỉ tác giả hoặc Admin). |
| Luồng chính | 1. Client gửi `GET /api/v1/recipes/{recipeId}/comments?page=1&pageSize=10` (token không bắt buộc).<br>2. `GetRecipeCommentsQuery` qua MediatR, `ValidationBehavior` kiểm tra page ≥ 1, pageSize 1–50.<br>3. Handler kiểm tra quyền xem công thức bằng `RecipeAuthorization.CanRead`.<br>4. Đếm tổng số, lấy 1 trang sắp theo `CreatedAt` giảm dần, include người viết.<br>5. Trả 200 với `{ data: CommentDto[], meta }`. |
| Luồng thay thế / Ngoại lệ | A1. Công thức không tồn tại, đã xoá, hoặc là bản nháp của người khác: 404 `RECIPE_NOT_FOUND` (không tiết lộ bản nháp có tồn tại).<br>A2. page/pageSize sai: 422. |
| HTTP Method & Endpoint | `GET /api/v1/recipes/{recipeId}/comments` |
| Kết quả mong đợi | Danh sách bình luận đúng thứ tự, đúng trang; không có bình luận đã xoá. |
| HTTP Status Code | 200 OK · 404 Not Found · 422 Unprocessable Entity |

### FR-CMT-002: Viết Bình luận

| Mục | Nội dung |
|---|---|
| Mã yêu cầu | FR-CMT-002 |
| Tên yêu cầu | Viết bình luận mới cho công thức |
| Nhóm chức năng | Module Bình luận Công thức (FR-CMT) |
| Tác nhân | Tác giả, Admin (đã đăng nhập) |
| Mức ưu tiên | M – Must Have |
| Mô tả | Người dùng đăng nhập viết bình luận dạng văn bản thuần dưới công thức **đã xuất bản**. Nội dung được trim và dài 1–1000 ký tự. |
| Điều kiện tiên quyết | 1. JWT hợp lệ, role Author hoặc Admin.<br>2. Tài khoản đang hoạt động (`IsActive = true`).<br>3. Công thức ở trạng thái Published. |
| Luồng chính | 1. Client gửi `POST /api/v1/recipes/{recipeId}/comments` với body `{ "content": "..." }`.<br>2. Rate limiter `comments`: tối đa 5 request/phút/người dùng.<br>3. `ValidationBehavior` chạy `AddCommentCommandValidator`: nội dung không rỗng sau trim, ≤ 1000 ký tự.<br>4. Handler kiểm tra công thức xem được và đang Published.<br>5. Kiểm tra tài khoản còn hoạt động (access token còn hạn tối đa 15 phút sau khi bị khoá).<br>6. Lưu `RecipeComment` (AuthorId lấy từ JWT, không lấy từ body).<br>7. Trả 201 Created với `CommentDto`. |
| Luồng thay thế / Ngoại lệ | A1. Chưa đăng nhập: 401.<br>A2. Nội dung rỗng hoặc > 1000 ký tự: 422 (ValidationProblemDetails).<br>A3. Công thức không tồn tại hoặc là bản nháp của người khác: 404.<br>A4. Công thức của chính mình nhưng chưa xuất bản: 409 "Chỉ bình luận được công thức đã xuất bản".<br>A5. Tài khoản đã bị khoá: 403.<br>A6. Vượt 5 bình luận/phút: 429 kèm header `Retry-After`. |
| HTTP Method & Endpoint | `POST /api/v1/recipes/{recipeId}/comments` |
| Kết quả mong đợi | Bình luận mới nằm đầu danh sách FR-CMT-001. |
| HTTP Status Code | 201 Created · 401 · 403 · 404 · 409 · 422 · 429 |

### FR-CMT-003: Sửa Bình luận

| Mục | Nội dung |
|---|---|
| Mã yêu cầu | FR-CMT-003 |
| Tên yêu cầu | Sửa bình luận của chính mình |
| Nhóm chức năng | Module Bình luận Công thức (FR-CMT) |
| Tác nhân | Người viết bình luận |
| Mức ưu tiên | S – Should Have |
| Mô tả | Chỉ người viết được sửa nội dung bình luận của mình. Admin cũng **không** được sửa lời người khác, chỉ được xoá (FR-CMT-004). Sửa xong, bình luận được đánh dấu `isEdited = true` và giao diện hiện "(đã chỉnh sửa)". |
| Điều kiện tiên quyết | Đã đăng nhập; bình luận tồn tại và chưa bị xoá. |
| Luồng chính | 1. `PUT /api/v1/comments/{commentId}` với body `{ "content": "..." }`.<br>2. Validator giống FR-CMT-002.<br>3. Handler kiểm tra `comment.AuthorId == UserId`.<br>4. Nếu nội dung thay đổi: cập nhật `Content`, `IsEdited = true`, lưu DB (trigger cập nhật `UpdatedAt`).<br>5. Trả 200 với `CommentDto` mới. |
| Luồng thay thế / Ngoại lệ | A1. Chưa đăng nhập: 401.<br>A2. Không phải người viết (kể cả Admin): 403.<br>A3. Không tìm thấy hoặc đã xoá: 404.<br>A4. Nội dung không hợp lệ: 422. |
| HTTP Method & Endpoint | `PUT /api/v1/comments/{commentId}` |
| Kết quả mong đợi | Nội dung mới hiển thị, có nhãn "đã chỉnh sửa". |
| HTTP Status Code | 200 OK · 401 · 403 · 404 · 422 |

### FR-CMT-004: Xoá Bình luận

| Mục | Nội dung |
|---|---|
| Mã yêu cầu | FR-CMT-004 |
| Tên yêu cầu | Xoá bình luận |
| Nhóm chức năng | Module Bình luận Công thức (FR-CMT) |
| Tác nhân | Người viết bình luận; tác giả công thức (kiểm duyệt bài của mình); Admin |
| Mức ưu tiên | M – Must Have |
| Mô tả | Xoá mềm (`IsDeleted = true`, `DeletedAt = now`); bình luận biến mất khỏi FR-CMT-001. Khi người xoá không phải người viết, hệ thống ghi log ai xoá bình luận của ai (FR-OBS-002). Khi công thức bị purge sau 30 ngày (FR-JOB-004), bình luận bị xoá theo nhờ FK `ON DELETE CASCADE`. |
| Điều kiện tiên quyết | Đã đăng nhập; bình luận tồn tại. |
| Luồng chính | 1. `DELETE /api/v1/comments/{commentId}`.<br>2. Handler kiểm tra quyền: người viết, tác giả công thức, hoặc Admin.<br>3. Đánh dấu xoá mềm, lưu DB.<br>4. Trả 204 No Content. |
| Luồng thay thế / Ngoại lệ | A1. Chưa đăng nhập: 401.<br>A2. Không thuộc 3 nhóm trên: 403.<br>A3. Không tìm thấy: 404. |
| HTTP Method & Endpoint | `DELETE /api/v1/comments/{commentId}` |
| Kết quả mong đợi | Bình luận không còn hiển thị; dữ liệu vẫn còn trong DB (khôi phục được). |
| HTTP Status Code | 204 No Content · 401 · 403 · 404 |

---

## 3.9. Module Quản trị Người dùng (FR-USR)

### FR-USR-001: Xem Danh sách Người dùng [Admin]

| Mục | Nội dung |
|---|---|
| Mã yêu cầu | FR-USR-001 |
| Tên yêu cầu | Xem danh sách người dùng (Admin, tác giả) |
| Tác nhân | Admin |
| Mức ưu tiên | S – Should Have |
| Mô tả | Trang `/dashboard/users` liệt kê tài khoản, mới nhất trước. Mỗi dòng gồm tên, email, vai trò, trạng thái, ngày tham gia, số công thức và số công thức đã xuất bản. Không bao giờ trả `PasswordHash` hay `SecurityStamp`. |
| Luồng chính | `GET /api/v1/users?q=&role=Admin\|Author&isActive=true\|false&page=1&pageSize=20`:<br>- `q` tìm trong email, tài khoản, tên hiển thị, không phân biệt hoa thường.<br>- `role=Author` là tác giả thường (không phải Admin).<br>- pageSize 1–50. |
| Ngoại lệ | Chưa đăng nhập: 401. Không phải Admin: 403. Tham số sai (role khác Admin/Author, pageSize > 50): 422. |
| HTTP Status Code | 200 · 401 · 403 · 422 |

### FR-USR-002: Khoá / Mở khoá Tài khoản [Admin]

| Mục | Nội dung |
|---|---|
| Mã yêu cầu | FR-USR-002 |
| Tên yêu cầu | Khoá hoặc mở khoá tài khoản |
| Tác nhân | Admin |
| Mức ưu tiên | S – Should Have |
| Mô tả | `PATCH /api/v1/users/{id}/status` với body `{ "isActive": false\|true }`. Khi khoá:<br>- `IsActive = false`.<br>- Thu hồi mọi refresh token của người đó.<br>- Từ lúc đó Login, Refresh, Google Login và viết bình luận đều trả 403.<br>Công thức của người bị khoá vẫn giữ nguyên. Mỗi lần khoá/mở khoá đều ghi log. |
| Ngoại lệ | Admin tự khoá chính mình: 403. Không tìm thấy: 404. Không phải Admin: 403. |
| HTTP Status Code | 200 · 401 · 403 · 404 |

---

## Phụ lục D. Dữ liệu & API bổ sung

### D.1. Bảng `RecipeComments` (db/init/02-schema.sql)

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Id | uuid | PK, `gen_random_uuid()` |
| RecipeId | uuid | NOT NULL, FK → `Recipes(Id)` ON DELETE CASCADE |
| AuthorId | varchar(450) | NOT NULL, FK → `AspNetUsers(Id)` ON DELETE CASCADE |
| Content | varchar(1000) | NOT NULL |
| IsEdited | boolean | NOT NULL, mặc định false |
| CreatedAt / UpdatedAt | timestamptz | Audit (trigger `touch_row`) |
| IsDeleted / DeletedAt | boolean / timestamptz | Xoá mềm, query filter `!IsDeleted` |
| RowVersion | bytea | Optimistic concurrency |

Index: `(RecipeId, CreatedAt)` cho FR-CMT-001; `(AuthorId)`. Database tạo trước v1.1.0 được tự thêm bảng lúc API khởi động (lệnh `CREATE TABLE IF NOT EXISTS` trong Program.cs).

### D.2. Endpoint bổ sung

| Method | Endpoint | Auth | Response |
|---|---|---|---|
| GET | `/api/v1/recipes/{recipeId}/comments` | Không | 200 `CommentDto[]` + meta |
| POST | `/api/v1/recipes/{recipeId}/comments` | Author/Admin, 5/phút | 201 `CommentDto` |
| PUT | `/api/v1/comments/{commentId}` | Người viết | 200 `CommentDto` |
| DELETE | `/api/v1/comments/{commentId}` | Người viết / tác giả công thức / Admin | 204 |
| GET | `/api/v1/users` | Admin | 200 `UserSummaryDto[]` + meta |
| PATCH | `/api/v1/users/{id}/status` | Admin | 200 `UserSummaryDto` |

`CommentDto` = `{ id, recipeId, content, author: { id, displayName, avatarUrl }, createdAt, updatedAt, isEdited, canEdit, canDelete }`.

### D.3. Truy vết (Traceability)

| FR | Backend | Frontend | Test |
|---|---|---|---|
| FR-CMT-001..004 | `Features/Comments/**`, `Endpoints/CommentEndpoints.cs`, `RecipeCommentConfiguration.cs` | `components/recipe/RecipeComments.tsx`, `hooks/useComments.ts`, `lib/comments.ts` | `UnitTests/Comments/RecipeCommentTests.cs`, `OperationsSecurityTests.CommentsApi_WritingRequiresLogin`, `frontend/tests/comments.test.mjs` |
| FR-USR-001..002 | `Features/Users/**`, `Endpoints/UsersEndpoints.cs` | `app/dashboard/users/page.tsx`, `hooks/useUsers.ts` | `UnitTests/Users/UserManagementTests.cs`, `OperationsSecurityTests.UsersApi_OnlyAdmin_*` |
