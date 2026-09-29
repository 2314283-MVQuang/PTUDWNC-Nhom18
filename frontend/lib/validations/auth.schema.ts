import { z } from "zod";

/**
 * Schema đăng ký:
 * - Tài khoản: chuỗi bất kỳ (ví dụ: 2312, hotien, admin...)
 * - Mật khẩu: chuỗi bất kỳ (ví dụ: 123)
 * - Xác minh lại mật khẩu: khớp 100% với mật khẩu
 */
export const registerSchema = z
  .object({
    account: z.string().min(1, "Vui lòng nhập tên tài khoản"),
    password: z.string().min(1, "Vui lòng nhập mật khẩu"),
    confirmPassword: z.string().min(1, "Vui lòng xác minh lại mật khẩu"),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Mật khẩu xác minh không khớp. Vui lòng kiểm tra lại!",
    path: ["confirmPassword"],
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
