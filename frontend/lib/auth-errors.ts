/**
 * Mã lỗi đăng nhập/đăng ký dùng chung giữa server (auth.ts, route /api/auth/register) và client
 * (LoginForm, RegisterForm). File này KHÔNG import gì để chạy được cả ở Edge, Node lẫn trình duyệt.
 *
 * Tuần 7 (Quang): trước đây mọi lỗi đăng nhập đều hiện "Email hoặc mật khẩu không đúng." — kể cả
 * khi backend chưa chạy hay tài khoản đang bị khoá — khiến người dùng (và người test) không biết
 * phải làm gì. Giờ phân biệt rõ từng trường hợp theo status backend trả về (RFC 7807).
 */
export const AUTH_ERROR = {
  /** 401 — sai email/mật khẩu (không tiết lộ email có tồn tại hay không — FR-AUTH-002). */
  invalidCredentials: "invalid_credentials",
  /** 423 — Identity khoá 15 phút sau 5 lần sai (LockedOutException). */
  accountLocked: "account_locked",
  /** 429 — vượt rate limit 10 request/phút/IP của nhóm /auth. */
  rateLimited: "rate_limited",
  /** Không gọi được backend (chưa chạy, sai URL) hoặc backend lỗi 5xx. */
  backendUnavailable: "backend_unavailable",
} as const;

export type AuthErrorCode = (typeof AUTH_ERROR)[keyof typeof AUTH_ERROR];

const LOGIN_MESSAGES: Record<AuthErrorCode, string> = {
  invalid_credentials: "Email hoặc mật khẩu không đúng.",
  account_locked: "Tài khoản tạm khoá do đăng nhập sai quá 5 lần. Vui lòng thử lại sau 15 phút.",
  rate_limited: "Bạn thao tác quá nhanh. Vui lòng đợi 1 phút rồi thử lại.",
  backend_unavailable: "Không kết nối được máy chủ. Vui lòng thử lại sau.",
};

/** Đổi HTTP status của backend thành mã lỗi đăng nhập. `undefined` = lỗi mạng (fetch throw). */
export function authErrorFromStatus(status: number | undefined): AuthErrorCode {
  switch (status) {
    case 400:
    case 401:
    case 422:
      return AUTH_ERROR.invalidCredentials;
    case 423:
      return AUTH_ERROR.accountLocked;
    case 429:
      return AUTH_ERROR.rateLimited;
    default:
      return AUTH_ERROR.backendUnavailable;
  }
}

/** Thông báo hiển thị trên form đăng nhập cho mã lỗi Auth.js trả về (`result.code`). */
export function loginErrorMessage(code: string | null | undefined): string {
  return code && code in LOGIN_MESSAGES
    ? LOGIN_MESSAGES[code as AuthErrorCode]
    : LOGIN_MESSAGES.invalid_credentials;
}
