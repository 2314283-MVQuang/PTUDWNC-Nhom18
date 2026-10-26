import { apiFetch } from "@/lib/api-client";
import type { RecipeImage } from "@/types/recipe";

/**
 * FR-RCP-008 + FR-FILE-001 — tải ảnh công thức lên theo luồng presigned URL của backend:
 *
 *   1. POST /files/presigned-url { fileName, contentType, size } → backend kiểm đuôi/loại/dung
 *      lượng, lưu metadata (bảng UploadedFiles) và trả `uploadUrl` (PUT, hết hạn 15 phút) +
 *      `publicUrl`.
 *   2. Trình duyệt PUT thẳng file lên MinIO bằng `uploadUrl` — file không đi qua API .NET.
 *   3. POST /recipes/{id}/images { imageUrl: publicUrl } để gắn ảnh vào công thức. Ảnh đầu tiên
 *      backend tự đặt làm ảnh đại diện.
 *
 * Trước đây frontend gửi multipart thẳng vào /recipes/{id}/images — endpoint đó chỉ nhận JSON
 * nên upload luôn lỗi.
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
  const invalid = validateImageFile(file);
  if (invalid) throw new StorageUploadError(invalid);

  // 1. Xin URL upload.
  const presigned = await apiFetch<PresignedUpload>("/files/presigned-url", {
    method: "POST",
    body: { fileName: file.name, contentType: file.type, size: file.size },
    token,
  });

  // 2. PUT file lên MinIO. Content-Type phải trùng loại đã ký trong URL, nếu không MinIO từ chối.
  let storageResponse: Response;
  try {
    storageResponse = await fetchImpl(presigned.uploadUrl, {
      method: "PUT",
      headers: { "Content-Type": presigned.contentType },
      body: file,
    });
  } catch {
    throw new StorageUploadError(
      "Không kết nối được kho lưu trữ ảnh (MinIO). Kiểm tra MinIO đang chạy và đã cho phép CORS.",
    );
  }
  if (!storageResponse.ok) throw new StorageUploadError();

  // 3. Gắn ảnh vào công thức.
  return apiFetch<RecipeImage>(`/recipes/${recipeId}/images`, {
    method: "POST",
    body: { imageUrl: presigned.publicUrl, altText: altText?.trim() || null },
    token,
  });
}
