import { ApiError, apiErrorMessage, apiFetch } from "@/lib/api-client";
import { AUTH_ERROR, authErrorFromStatus, type AuthErrorCode } from "@/lib/auth-errors";
import type { AuthTokenResponse, RegisterInput } from "@/types/user";

/**
 * Tuần 7 (Quang) — FR-AUTH-001/002 phía frontend: mọi lời gọi tới nhóm endpoint /auth của backend
 * .NET gom về đây, để auth.ts và route /api/auth/register chỉ còn việc nối dây.
 *
 * Backend là nguồn xác thực DUY NHẤT: tài khoản đăng ký trên giao diện được lưu vào PostgreSQL
 * (bảng AspNetUsers) qua POST /auth/register; đăng nhập lấy JWT thật qua POST /auth/login. Danh
 * sách user cục bộ (data/users.json, token "local-token-...") chỉ còn dùng khi bật demo rõ ràng
 * (AUTH_DEMO_ENABLED=true ở Development — xem lib/auth-mode.ts).
 */

/** Lỗi đăng nhập kèm mã để Auth.js chuyển thẳng xuống form (CredentialsSignin.code). */
export class BackendAuthError extends Error {
  readonly code: AuthErrorCode;

  constructor(code: AuthErrorCode) {
    super(code);
    this.name = "BackendAuthError";
    this.code = code;
  }
}

/** User mà `authorize()` trả về cho Auth.js — callback `jwt` ở auth.config.ts đọc các field này. */
export interface BackendSessionUser {
  id: string;
  email: string;
  name: string;
  image: string | null;
  roles: string[];
  accessToken: string;
  refreshToken: string;
  accessTokenExpires: number;
}

/**
 * Chuyển response của /auth/login (hoặc /auth/register) thành user cho Auth.js.
 * Response đã kèm `user` (AuthResponseDto) nên KHÔNG cần gọi thêm GET /auth/me — mỗi lần đăng nhập
 * chỉ tốn 1 request vào nhóm /auth (rate limit 10 request/phút/IP).
 */
export function toSessionUser(tokens: AuthTokenResponse, now: number = Date.now()): BackendSessionUser {
  return {
    id: tokens.user.id,
    email: tokens.user.email,
    name: tokens.user.displayName,
    image: tokens.user.avatarUrl ?? null,
    roles: [...(tokens.user.roles ?? [])],
    accessToken: tokens.accessToken,
    refreshToken: tokens.refreshToken,
    accessTokenExpires: now + tokens.expiresIn * 1000,
  };
}

/** FR-AUTH-002: POST /auth/login. Ném `BackendAuthError` với mã lỗi rõ ràng khi thất bại. */
export async function loginWithBackend(email: string, password: string): Promise<BackendSessionUser> {
  let tokens: AuthTokenResponse;
  try {
    tokens = await apiFetch<AuthTokenResponse>("/auth/login", {
      method: "POST",
      body: { email, password },
      cache: "no-store",
    });
  } catch (error) {
    throw new BackendAuthError(
      error instanceof ApiError ? authErrorFromStatus(error.status) : AUTH_ERROR.backendUnavailable,
    );
  }
  return toSessionUser(tokens);
}

/** FR-AUTH-001: POST /auth/register — backend tạo user trong PostgreSQL và gán role Author. */
export function registerWithBackend(input: RegisterInput): Promise<AuthTokenResponse> {
  return apiFetch<AuthTokenResponse>("/auth/register", { method: "POST", body: input, cache: "no-store" });
}

/**
 * Đổi lỗi đăng ký thành { status, message } cho route /api/auth/register.
 * - 409: email đã được sử dụng (ConflictException).
 * - 422: lỗi validate — ưu tiên thông báo theo từng field (vd. mật khẩu thiếu chữ hoa).
 * - 429: vượt rate limit.
 * - còn lại / lỗi mạng: 503, thông báo chung, không lộ chi tiết máy chủ.
 */
export function registerErrorResponse(error: unknown): { status: number; message: string } {
  if (error instanceof ApiError) {
    if (error.status === 429) return { status: 429, message: "Bạn thao tác quá nhanh. Vui lòng đợi 1 phút rồi thử lại." };
    if (error.status >= 400 && error.status < 500)
      return { status: error.status, message: apiErrorMessage(error, "Đăng ký thất bại, vui lòng kiểm tra lại thông tin.") };
  }
  return { status: 503, message: "Không kết nối được máy chủ. Vui lòng thử lại sau." };
}

/**
 * Đăng xuất: thu hồi refresh token ở backend (POST /auth/logout, cần Bearer) để token bị lộ không
 * dùng tiếp được. Lỗi ở bước này không được chặn việc đăng xuất phía giao diện nên chỉ bỏ qua.
 */
export async function revokeBackendSession(accessToken: unknown, refreshToken: unknown): Promise<boolean> {
  if (typeof accessToken !== "string" || typeof refreshToken !== "string") return false;
  if (accessToken.startsWith("local-token-")) return false; // token demo không tồn tại ở backend
  try {
    await apiFetch<void>("/auth/logout", {
      method: "POST",
      token: accessToken,
      body: { refreshToken },
      cache: "no-store",
    });
    return true;
  } catch {
    return false;
  }
}
