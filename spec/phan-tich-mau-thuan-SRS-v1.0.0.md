# Phân tích mâu thuẫn trong SRS Culinary Blog v1.0.0 + Hướng giải quyết

Tài liệu này ghi lại các mâu thuẫn/nghịch lý phát hiện được trong `SRS_Culinary_Blog_v1.0.0.pdf` (71 trang) và **quyết định chốt** cho từng mục để nhóm code theo thống nhất. Mỗi mục có trích dẫn chương/trang để đối chiếu lại tài liệu gốc.

## 1. Soft delete vs Hard delete cho Recipe — mâu thuẫn nghiêm trọng nhất

**Mâu thuẫn:**
- Soft delete: mở đầu Chương 7 (tr.54), bảng BaseEntity (tr.54), bảng Recipe (tr.55), NFR-REL-003 (tr.43), bảng API 8.3 (tr.64, "soft delete").
- Hard delete: FR-RCP-007 (tr.32) — `_unitOfWork.Recipes.Remove(recipe)`, xóa vật lý + xóa file MinIO vĩnh viễn.

**Chốt: dùng cả hai theo 2 tầng (soft delete trước, hard purge sau) — không phải chọn 1 trong 2.**
- `DELETE /recipes/{id}` chỉ set `IsDeleted = true` (đúng NFR-REL-003, đúng BaseEntity pattern, đúng nhãn "soft delete" ở API 8.3). Recipe biến mất khỏi mọi query công khai (Global Query Filter), owner/admin vẫn có thể khôi phục nếu cần (chưa cần làm UI khôi phục ngay, nhưng để mở khả năng).
- Không xóa file MinIO ngay lúc này.
- Thêm 1 Hangfire Recurring Job (đặt tên `PurgeDeletedRecipesJob`, chạy hàng ngày) quét các Recipe có `IsDeleted = true` quá N ngày (đề xuất 30 ngày) → mới thực sự `Remove()` khỏi DB (cascade Steps/Ingredients/Images) và xóa file trên MinIO. Đây chính là lúc áp dụng đúng phần kỹ thuật chi tiết mà FR-RCP-007 đã viết, chỉ là dịch nó vào background job thay vì chạy ngay khi user bấm Delete.
- Sửa lại FR-RCP-007 trong SRS: đổi mô tả từ "hard delete ngay" thành "soft delete ngay + hard purge sau N ngày qua background job".
- Lý do chọn hướng này: giữ được "có thể khôi phục" (đáp ứng NFR-REL-003, tránh mất dữ liệu do bấm nhầm) mà vẫn dọn được dữ liệu/file rác về sau (đáp ứng đúng lo ngại ban đầu của FR-RCP-007 về việc không để rác tồn mãi).

## 2. Category delete

**Chốt:** áp dụng soft delete giống Recipe, cho đồng bộ toàn hệ thống (đúng như nhãn "(soft delete)" ở API 8.2).
- `DeleteCategoryCommand` set `IsDeleted = true`, không gọi `Remove()`.
- Rule "không xóa category còn chứa recipe" giữ nguyên, đếm recipe qua Global Query Filter (tự động loại recipe đã soft-delete) — không cần sửa logic đếm.
- Category không cần hard-purge job riêng vì category không có file đi kèm và số lượng rất nhỏ (giới hạn ≤ 50 category theo mục 2.6.1) — không tốn chi phí lưu trữ đáng kể nếu giữ lại vĩnh viễn ở trạng thái soft-deleted.

## 3. Chiến lược cache: 3 công nghệ khác nhau

**Chốt: chỉ dùng 1 tầng cache duy nhất — ASP.NET Core Output Cache, backend bằng Redis.**
- Bỏ hoàn toàn `IMemoryCache` (đúng yêu cầu NFR-SCALE-001, hỗ trợ scale-out nhiều instance).
- Bỏ luôn `CachingBehavior`/`CacheInvalidationBehavior` trong MediatR pipeline (tầng cache ở Application layer) — thay vào đó dùng Output Cache middleware ở Presentation layer cho tất cả GET endpoint public: `/categories`, `/categories/{slug}`, `/recipes`, `/recipes/{slug}`.
- Cấu hình Output Cache dùng `IOutputCacheStore` backed by Redis (package `Microsoft.AspNetCore.OutputCaching` + custom Redis store, hoặc dùng sẵn thư viện community) thay vì store in-memory mặc định — vừa đơn giản (1 kỹ thuật, tag-based invalidation `EvictByTagAsync` đã có sẵn), vừa thật sự "distributed" nên nhiều instance API vẫn thấy cùng 1 cache.
- `RedisCacheService` trong Infrastructure layer chỉ giữ lại cho các nhu cầu cache khác không phải HTTP response (ví dụ rate limiting counter ở NFR-SEC-003), không dùng cho category/recipe nữa.

## 4. TTL cache không khớp

**Chốt: lấy số liệu ở NFR-PERF-003 làm chuẩn duy nhất, sửa lại Chương 3 cho khớp:**
- Category list: **30 phút**.
- Recipe detail: **5 phút**.
- Search results: **1 phút** (bỏ luôn tùy chọn "không cache" ở FR-SRCH-001 để đơn giản hóa, vẫn cache ngắn 1 phút, vary theo full query string).
- Giữ nguyên cơ chế invalidate theo event (evict tag khi Create/Update/Delete/Publish) làm lớp bảo vệ thêm cho cả 3 loại — TTL ngắn + evict theo event kết hợp sẽ hạn chế tối đa dữ liệu cũ hiển thị sai.

## 5. Mã lỗi HTTP cho conflict RowVersion

**Chốt: dùng 409 Conflict**, không dùng 422.
- Lý do: theo đúng ngữ nghĩa HTTP, 409 dành cho "request xung đột với trạng thái hiện tại của resource" — đúng bản chất optimistic concurrency conflict. 422 nên dành riêng cho lỗi validate dữ liệu đầu vào (sai cú pháp/ngữ nghĩa field), không phải xung đột trạng thái.
- Sửa Phụ lục A và Phụ lục B (`RECIPE_CONCURRENCY_CONFLICT`): đổi từ 422 → 409, khớp với FR-RCP-004.

## 6. Xử lý trùng slug

**Chốt: Recipe tự sinh suffix giống Category (-2, -3...), không trả lỗi 409.**
- Sửa FR-RCP-003 Alternate Flow A3: bỏ nhánh "409 Conflict", thay bằng logic giống Category (`SlugHelper.Generate` + kiểm tra tồn tại + tăng suffix cho tới khi unique).
- Lý do: title trùng là chuyện bình thường (2 người cùng đăng "Phở bò" là hợp lý), không nên chặn user tạo recipe chỉ vì trùng tên — trải nghiệm tốt hơn khi hệ thống tự xử lý ngầm.
- `RECIPE_SLUG_EXISTS` trong Phụ lục B giữ lại nhưng chỉ dùng cho mục đích log nội bộ, không phải lỗi trả về client nữa (thực tế sẽ không bao giờ throw ra ngoài).

## 7. Điều kiện publish Recipe

**Chốt: yêu cầu cả ≥1 step VÀ ≥1 ingredient** (theo Phụ lục B, chặt hơn và hợp lý hơn).
- Sửa `Recipe.Publish()` domain method: kiểm tra `Steps.Count > 0 && Ingredients.Count > 0`, nếu thiếu 1 trong 2 → `DomainException` với message rõ thiếu gì.
- Sửa FR-RCP-005 business rule cho khớp.
- Lý do: recipe published sẽ được index SEO với JSON-LD Schema.org (`recipeIngredient[]` là field bắt buộc theo NFR-SEO-001) — thiếu ingredient thì structured data sai/thiếu, Google Rich Results Test sẽ fail.

## 8. Luồng Google OAuth

**Chốt: dùng Authorization Code Flow + PKCE (theo FR-AUTH-003), sửa lại API 8.1 cho khớp.**
- Lý do: Auth.js v5 (đã chọn làm thư viện auth ở Frontend theo mục 6.1) mặc định implement Google provider theo Authorization Code Flow, không phải flow nhận idToken trực tiếp từ Google Identity Services JS SDK. Dùng đúng cơ chế Auth.js hỗ trợ sẵn sẽ tiết kiệm code hơn nhiều so với tự tích hợp Google Sign-In JS SDK riêng.
- Sửa request body của `POST /auth/google` ở mục 8.1: đổi từ `{ idToken }` thành `{ email, displayName, avatarUrl, providerKey }` (thông tin Auth.js đã lấy được từ Google profile sau khi exchange code), khớp với luồng `GoogleLoginCommandHandler` đã mô tả ở FR-AUTH-003 bước 6–8.

## 9. Field đặt tên người dùng (fullName/userName vs displayName)

**Chốt: dùng `displayName` thống nhất toàn bộ (khớp cột DB thật `ApplicationUser.DisplayName`), bỏ hẳn `fullName`/`userName` riêng khỏi các DTO.**
- Sửa toàn bộ Chương 3 (FR-AUTH-001, FR-AUTH-006, FR-AUTH-007): đổi `fullName` → `displayName` trong mọi request/response/method signature (`ApplicationUser.Create(displayName, email, userName)` — `userName` (Identity's UserName, dùng để định danh đăng nhập) vẫn giữ riêng biệt với `displayName` (tên hiển thị công khai) vì hai khái niệm khác nhau, không gộp.
- Chốt `UserProfileDto` dùng chung cho cả FR-AUTH-006 và API 8.1, gồm đủ: `{ id, email, displayName, avatarUrl, bio, roles, emailConfirmed, createdAt }` — thêm `bio` (đang thiếu ở FR-AUTH-006) và giữ `emailConfirmed`/`createdAt` (đang thiếu ở 8.1).
- Sửa FR-AUTH-007: cho phép sửa cả `displayName`, `avatarUrl` VÀ `bio` (khớp với request body `{ displayName?, avatarUrl?, bio? }` đã có ở API 8.1) — không giới hạn chỉ 2 field như bản cũ.

## 10. Cột `Instructions` NOT NULL vs optional

**Chốt: đổi cột `Instructions` thành nullable (bỏ NOT NULL), giữ request field là optional như hiện tại.**
- Lý do: chính DB đã ghi chú field này là "legacy field", nội dung chi tiết thực sự nằm ở `RecipeStep` — ép NOT NULL cho 1 field được coi là legacy/phụ là không hợp lý. Sửa schema dễ hơn và ít rủi ro hơn việc bắt buộc mọi request phải gửi field không còn quan trọng.
- Update migration: `ALTER COLUMN "Instructions" DROP NOT NULL` (hoặc set default `''` nếu team ngại sửa migration, nhưng nullable là lựa chọn sạch hơn).

## 11. RefreshToken không thực sự kế thừa BaseEntity

**Chốt: giữ nguyên code (RefreshToken KHÔNG kế thừa BaseEntity), chỉ sửa lại câu chữ mô tả chung ở mục 6.4 và mở đầu Chương 7.**
- Lý do: RefreshToken là bản ghi audit/append-only — không cần soft delete (revoke xong thì giữ lại làm lịch sử, không cần "xóa" theo nghĩa UI), không cần optimistic concurrency (mỗi token chỉ bị revoke đúng 1 lần bởi đúng 1 luồng). Ép nó theo BaseEntity là thừa.
- Sửa câu ở 6.4/Chương 7 thành: "Hầu hết entities kế thừa BaseEntity; ngoại lệ là RefreshToken (không cần soft delete/concurrency do bản chất append-only)."

---

### Ghi chú nhỏ (mức độ thấp hơn)

**Bảng cột Category/RecipeStep/RecipeIngredient/RecipeImage thiếu liệt kê CreatedAt/UpdatedAt/IsDeleted/RowVersion** — chốt: các entity này **có** kế thừa đủ 4 cột BaseEntity như Recipe (chỉ là tài liệu lược bớt cho ngắn), khi update SRS nên bổ sung lại 4 dòng đó vào từng bảng cho rõ, tránh hiểu lầm khi review code với giảng viên/GVHD.
