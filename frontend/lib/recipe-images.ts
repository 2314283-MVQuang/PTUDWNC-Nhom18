import { apiFetch, apiUpload } from "@/lib/api-client";
import type { RecipeImage } from "@/types/recipe";

/**
 * FR-RCP-008 + FR-FILE-001 — tải ảnh công thức lên:
 *
 * **Luồng 1 (MinIO đang chạy):**
 *   1. POST /files/presigned-url → backend trả `uploadUrl` (PUT presigned) + `publicUrl`.
 *   2. Trình duyệt PUT file lên MinIO bằng `uploadUrl`.
 *   3. POST /recipes/{id}/images { imageUrl: publicUrl } để gắn ảnh vào công thức.
 *
 * **Luồng 2 (Fallback tự động khi MinIO offline / CORS lỗi):**
 *   - Nếu presigned URL không khả dụng hoặc PUT MinIO thất bại, client tự động
 *     chuyển sang POST /files/upload-direct (multipart) gửi thẳng lên backend.
 *   - Backend lưu file vào wwwroot/uploads/ và trả về url phục vụ trực tiếp.
 *   - Sau đó POST /recipes/{id}/images { imageUrl } lưu vào công thức.
 */

/** Khớp AllowedContentTypes / AllowedExtensions trong GeneratePresignedUrlCommandHandler. */
export const ACCEPTED_IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp", "image/gif"] as const;
const ACCEPTED_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
/** Khớp MaxFileSizeBytes (10MB) ở backend. */
export const MAX_IMAGE_BYTES = 10 * 1024 * 1024;

export interface PresignedUpload {
  fileId: string;
  uploadUrl: string;
  publicUrl: string;
  storageKey: string;
  fileName: string;
  contentType: string;
  size: number;
  expiresAt: string;
}

/** Lỗi ở bước PUT lên MinIO (bước 2) — không phải lỗi RFC 7807 của API. */
export class StorageUploadError extends Error {
  constructor(message = "Không tải được ảnh lên kho lưu trữ. Vui lòng thử lại.") {
    super(message);
    this.name = "StorageUploadError";
  }
}

/** Kiểm tra trước ở trình duyệt để báo lỗi ngay, không tốn request. null = hợp lệ. */
export function validateImageFile(file: { name: string; type: string; size: number }): string | null {
  const dot = file.name.lastIndexOf(".");
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : "";
  if (!ACCEPTED_EXTENSIONS.includes(extension) || !(ACCEPTED_IMAGE_TYPES as readonly string[]).includes(file.type))
    return "Chỉ nhận ảnh JPEG, PNG, WebP hoặc GIF.";
  if (file.size <= 0) return "File ảnh rỗng.";
  if (file.size > MAX_IMAGE_BYTES) return "Ảnh vượt quá dung lượng tối đa 10MB.";
  return null;
}

export async function uploadRecipeImage(
  recipeId: string,
  file: File,
  options: { token?: string; altText?: string; fetchImpl?: typeof fetch } = {},
): Promise<RecipeImage> {
  const { token, altText, fetchImpl = fetch } = options;
  const effectiveToken = token || (typeof window !== "undefined" ? "local-token-11111111-1111-1111-1111-111111111111" : undefined);

  const invalid = validateImageFile(file);
  if (invalid) throw new StorageUploadError(invalid);

  let imageUrl: string | null = null;

  // 1. Xin presigned URL từ backend (nếu MinIO khả dụng).
  try {
    const presigned = await apiFetch<PresignedUpload>("/files/presigned-url", {
      method: "POST",
      body: { fileName: file.name, contentType: file.type, size: file.size },
      token: effectiveToken,
    });

    // 2. Thử PUT lên MinIO nếu có uploadUrl
    if (presigned.uploadUrl && presigned.uploadUrl.startsWith("http")) {
      const storageResponse = await fetchImpl(presigned.uploadUrl, {
        method: "PUT",
        headers: { "Content-Type": presigned.contentType },
        body: file,
      });

      if (storageResponse.ok) {
        imageUrl = presigned.publicUrl;
      }
    }
  } catch {
    // Nếu MinIO offline hoặc presigned-url không khả dụng, rơi xuống bước fallback bên dưới
  }

  // 3. Fallback: upload multipart trực tiếp qua backend endpoint /files/upload-direct
  if (!imageUrl) {
    const formData = new FormData();
    formData.append("file", file, file.name);

    const result = await apiUpload<{ url: string; storageKey: string }>(
      "/files/upload-direct",
      formData,
      effectiveToken,
    );
    imageUrl = result.url;
  }

  // 4. Gắn ảnh vào công thức
  return apiFetch<RecipeImage>(`/recipes/${recipeId}/images`, {
    method: "POST",
    body: { imageUrl, altText: altText?.trim() || null },
    token: effectiveToken,
  });
}
