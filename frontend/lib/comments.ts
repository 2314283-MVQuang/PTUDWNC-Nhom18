/**
 * FR-CMT (Tuần 9) — quy tắc nội dung bình luận dùng chung cho form tạo/sửa.
 * Không import gì để test bằng node:test được (xem tests/comments.test.mjs).
 */

/** Khớp RecipeComment.MaxContentLength ở backend. */
export const COMMENT_MAX_LENGTH = 1000;

/** Kiểm tra trước ở trình duyệt, khớp validator backend (trim, 1–1000 ký tự). null = hợp lệ. */
export function validateCommentContent(content: string): string | null {
  const trimmed = content.trim();
  if (!trimmed) return "Nội dung bình luận không được để trống.";
  if (trimmed.length > COMMENT_MAX_LENGTH) return `Bình luận tối đa ${COMMENT_MAX_LENGTH} ký tự.`;
  return null;
}
