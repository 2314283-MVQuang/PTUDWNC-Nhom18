import NextAuth, { CredentialsSignin } from "next-auth";
import Credentials from "next-auth/providers/credentials";
import Google from "next-auth/providers/google";
import { AUTH_ERROR, type AuthErrorCode } from "@/lib/auth-errors";
import { BackendAuthError, loginWithBackend, revokeBackendSession } from "@/lib/backend-auth";

/**
 * Cấu hình Auth.js v5 (mục 6.1, 6.3: đăng nhập email/password + Google OAuth).
 *
 * Ý tưởng: Next.js KHÔNG tự xác thực — nó chỉ là "cầu nối" gọi sang backend .NET
 * (nguồn xác thực thật, xem FR-AUTH-001..007), rồi lưu accessToken/refreshToken
 * nhận về vào JWT session của Auth.js để dùng lại cho các lần gọi API sau.
 *
 * - accessToken: JWT 15 phút (CONS-004), gắn vào header Authorization khi gọi API.
 * - refreshToken: 7 ngày, dùng để xin accessToken mới khi hết hạn (FR-AUTH-004, có rotation).
 */
import { findLocalUser } from "@/lib/local-users";
import { authConfig } from "@/auth.config";
import { isDemoAuthEnabled } from "@/lib/auth-mode";

/** Auth.js chuyển `code` của lỗi này xuống client: `signIn(...)` trả `{ error, code }`. */
class BackendCredentialsSignin extends CredentialsSignin {
  constructor(code: AuthErrorCode) {
    super();
    this.code = code;
  }
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  ...authConfig,
  events: {
    // Đăng xuất (Tuần 7): thu hồi refresh token ở backend (POST /auth/logout) — không để token cũ
    // còn dùng được 7 ngày sau khi người dùng đã bấm "Đăng xuất".
    async signOut(message) {
      const token = "token" in message ? await message.token : null;
      if (token) await revokeBackendSession(token.accessToken, token.refreshToken);
    },
  },
  providers: [
    Credentials({
      credentials: {
        account: { label: "Tài khoản", type: "text" },
        email: { label: "Email", type: "text" },
        password: { label: "Mật khẩu", type: "password" },
      },
      async authorize(credentials) {
        const identifier = (
          (credentials?.account as string | undefined) ||
          (credentials?.email as string | undefined)
        )?.trim();
        const password = credentials?.password as string | undefined;
        if (!identifier || !password) return null;

        // 1. Ưu tiên kiểm tra tài khoản mẫu trong đề & tài khoản vừa đăng ký (2312, admin, author...)
        const localUser = isDemoAuthEnabled() ? findLocalUser(identifier, password) : null;
        if (localUser) {
          return {
            id: localUser.id,
            email: localUser.email,
            name: localUser.displayName,
            image: localUser.avatarUrl ?? null,
            roles: localUser.roles,
            accessToken: "local-token-" + localUser.id,
            refreshToken: "local-refresh-" + localUser.id,
            accessTokenExpires: Date.now() + 24 * 60 * 60 * 1000,
          };
        }

        // 2. Backend .NET là nguồn xác thực thật (Tuần 7 — FR-AUTH-002): POST /auth/login trả JWT
        //    + refresh token + hồ sơ user trong CÙNG 1 response. Lỗi được đổi thành mã riêng
        //    (sai mật khẩu / tài khoản khoá / quá giới hạn / máy chủ không phản hồi) để form hiển
        //    thị đúng nguyên nhân thay vì luôn báo "sai mật khẩu".
        try {
          return await loginWithBackend(identifier, password);
        } catch (error) {
          throw new BackendCredentialsSignin(
            error instanceof BackendAuthError ? error.code : AUTH_ERROR.backendUnavailable,
          );
        }
      },
    }),
    // FR-AUTH-003: Đăng nhập Google OAuth 2.0. Chỉ bật khi có đủ env — tránh crash lúc dev
    // nếu intern chưa xin được Google Client ID/Secret.
    ...(process.env.AUTH_GOOGLE_ID && process.env.AUTH_GOOGLE_SECRET
      ? [Google({ clientId: process.env.AUTH_GOOGLE_ID, clientSecret: process.env.AUTH_GOOGLE_SECRET })]
      : []),
  ],
});
