import { z } from "zod";

/**
 * Schema đăng ký:
 * - Tài khoản: chuỗi bất kỳ (ví dụ: 2312, hotien, admin...)
 * - Mật khẩu: chuỗi bất kỳ (ví dụ: 123)
 * - Xác minh lại mật khẩu: khớp 100% với mật khẩu
 */
export const demoRegisterSchema = z
  .object({
    account: z.string().min(1, "Vui lòng nhập tên tài khoản"),
    email: z.string().optional(),
    password: z.string().min(1, "Vui lòng nhập mật khẩu"),
    confirmPassword: z.string().min(1, "Vui lòng xác minh lại mật khẩu"),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Mật khẩu xác minh không khớp. Vui lòng kiểm tra lại!",
    path: ["confirmPassword"],
  });

export const registerSchema = z.object({
  account: z.string().trim().min(3, "Tên tài khoản cần ít nhất 3 ký tự").max(50)
    .regex(/^[a-zA-Z0-9_.]+$/, "Tài khoản chỉ gồm chữ, số, dấu gạch dưới và dấu chấm"),
  fullName: z.string().trim().min(1, "Vui lòng nhập họ tên").max(100),
  email: z.string().trim().email("Email không hợp lệ"),
  password: z.string().min(8, "Mật khẩu cần ít nhất 8 ký tự")
    .regex(/[A-Z]/, "Mật khẩu cần chữ hoa").regex(/[0-9]/, "Mật khẩu cần chữ số")
    .regex(/[!@#$%^&*(),.?":{}|<>_\-+=\[\]/\\;'~`]/, "Mật khẩu cần ký tự đặc biệt"),
  confirmPassword: z.string().min(1, "Vui lòng xác nhận mật khẩu"),
}).refine(data => data.password === data.confirmPassword, {
  message: "Mật khẩu xác nhận không khớp", path: ["confirmPassword"],
});

export type RegisterFormValues = z.infer<typeof registerSchema>;

/**
 * Schema đăng nhập:
 * - Tài khoản: tên đăng nhập hoặc email (ví dụ: 2312, admin@culinaryblog.local)
 * - Mật khẩu: mật khẩu (ví dụ: 123, Admin@123)
 */
export const loginSchema = z.object({
  account: z.string().min(1, "Vui lòng nhập tài khoản hoặc email"),
  password: z.string().min(1, "Vui lòng nhập mật khẩu"),
});

export type LoginFormValues = z.infer<typeof loginSchema>;

export const updateProfileSchema = z.object({
  displayName: z.string().min(1, "Vui lòng nhập tên hiển thị"),
  bio: z.string().max(500, "Giới thiệu tối đa 500 ký tự").optional(),
  avatarUrl: z.string().url("URL ảnh không hợp lệ").optional().or(z.literal("")),
});
export type UpdateProfileFormValues = z.infer<typeof updateProfileSchema>;

// ---------------------------------------------------------------------------
// Auth nâng cao (Tuần 3 backend, Tuần 8 frontend — Quang): quên/đặt lại/đổi mật khẩu, xác nhận
// email. Chính sách mật khẩu khớp RegisterCommandValidator/ChangePassword/ResetPassword ở backend.
// ---------------------------------------------------------------------------

const strongPassword = z.string().min(8, "Mật khẩu cần ít nhất 8 ký tự")
  .regex(/[A-Z]/, "Mật khẩu cần chữ hoa").regex(/[0-9]/, "Mật khẩu cần chữ số")
  .regex(/[!@#$%^&*(),.?":{}|<>_\-+=\[\]/\;'~`]/, "Mật khẩu cần ký tự đặc biệt");

/**
 * Token Identity là base64 nên có thể chứa "+"; khi dán từ link không mã hoá, "+" trong query
 * string bị đọc thành dấu cách — đổi lại cho đúng trước khi gửi.
 */
export function normalizeAuthToken(token: string): string {
  return token.trim().replace(/ /g, "+");
}

export const forgotPasswordSchema = z.object({
  email: z.string().trim().email("Email không hợp lệ"),
});
export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z.object({
  email: z.string().trim().email("Email không hợp lệ"),
  token: z.string().trim().min(1, "Vui lòng nhập mã đặt lại mật khẩu"),
  newPassword: strongPassword,
  confirmPassword: z.string().min(1, "Vui lòng xác nhận mật khẩu"),
}).refine(data => data.newPassword === data.confirmPassword, {
  message: "Mật khẩu xác nhận không khớp", path: ["confirmPassword"],
});
export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

export const changePasswordSchema = z.object({
  currentPassword: z.string().min(1, "Vui lòng nhập mật khẩu hiện tại"),
  newPassword: strongPassword,
  confirmPassword: z.string().min(1, "Vui lòng xác nhận mật khẩu"),
}).refine(data => data.newPassword === data.confirmPassword, {
  message: "Mật khẩu xác nhận không khớp", path: ["confirmPassword"],
}).refine(data => data.newPassword !== data.currentPassword, {
  message: "Mật khẩu mới phải khác mật khẩu hiện tại", path: ["newPassword"],
});
export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>;

export const confirmEmailSchema = z.object({
  userId: z.string().trim().min(1, "Vui lòng nhập mã người dùng"),
  token: z.string().trim().min(1, "Vui lòng nhập mã xác nhận"),
});
export type ConfirmEmailFormValues = z.infer<typeof confirmEmailSchema>;
