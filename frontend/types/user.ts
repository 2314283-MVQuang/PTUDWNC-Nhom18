/** Vai trò người dùng (mục 2). "Guest" không xuất hiện ở đây vì Guest không có tài khoản. */
export type UserRole = "Author" | "Admin";

/** Khớp `UserProfileDto` trả về từ GET /auth/me (mục 8.1) — KHÔNG BAO GIỜ có PasswordHash. */
export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  bio: string | null;
  roles: UserRole[];
}

/** Body gửi lên PATCH /auth/me (FR-AUTH-007) — chỉ 3 field này được phép đổi. */
export interface UpdateProfileInput {
  displayName?: string;
  avatarUrl?: string;
  bio?: string;
}

/** Response của /auth/login, /auth/register, /auth/refresh (mục 8.1). */
export interface AuthTokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: UserProfile;
}

export interface LoginInput { email: string; password: string }
export interface RegisterInput { fullName: string; email: string; userName?: string; password: string }
export interface RefreshTokenInput { refreshToken: string }
export interface RegistrationResponse {
  success?: boolean;
  error?: string;
  user?: Pick<UserProfile, "id" | "email" | "displayName">;
}
