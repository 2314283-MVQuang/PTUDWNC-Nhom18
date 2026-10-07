import NextAuth from "next-auth";
import Credentials from "next-auth/providers/credentials";
import Google from "next-auth/providers/google";
import { apiFetch, ApiError } from "@/lib/api-client";
import type { AuthTokenResponse, UserProfile } from "@/types/user";

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

export const { handlers, auth, signIn, signOut } = NextAuth({
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
        const localUser = findLocalUser(identifier, password);
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

        // 2. Gọi backend .NET (nếu backend đang chạy)
        try {
          // FR-AUTH-002: POST /auth/login
          const tokens = await apiFetch<AuthTokenResponse>("/auth/login", {
            method: "POST",
            body: { email: identifier, password },
          });
          // Lấy hồ sơ user để hiển thị tên/avatar mà không cần gọi thêm lần nữa ở client.
          const profile = await apiFetch<UserProfile>("/auth/me", { token: tokens.accessToken });

          return {
            id: profile.id,
            email: profile.email,
            name: profile.displayName,
            image: profile.avatarUrl,
            roles: profile.roles,
            accessToken: tokens.accessToken,
            refreshToken: tokens.refreshToken,
            accessTokenExpires: Date.now() + tokens.expiresIn * 1000,
          };
        } catch {
          // Khi backend chưa chạy hoặc thông tin đăng nhập sai -> trả null để NextAuth báo lỗi thân thiện
          return null;
        }
      },
    }),
    // FR-AUTH-003: Đăng nhập Google OAuth 2.0. Chỉ bật khi có đủ env — tránh crash lúc dev
    // nếu intern chưa xin được Google Client ID/Secret.
    ...(process.env.AUTH_GOOGLE_ID && process.env.AUTH_GOOGLE_SECRET
      ? [Google({ clientId: process.env.AUTH_GOOGLE_ID, clientSecret: process.env.AUTH_GOOGLE_SECRET })]
      : []),
  ],
  pages: {
    signIn: "/login",
  },
  session: { strategy: "jwt" },
  callbacks: {
    async jwt({ token, user, account }) {
      // Lần đăng nhập đầu tiên bằng Credentials: `user` là object trả về từ `authorize()` ở trên.
      if (user) {
        token.accessToken = user.accessToken as string;
        token.refreshToken = user.refreshToken as string;
        token.accessTokenExpires = user.accessTokenExpires as number;
        token.roles = user.roles as string[];
      }

      // Lần đăng nhập đầu tiên bằng Google: trao đổi id_token của Google lấy token của backend.
      if (account?.provider === "google" && account.id_token) {
        try {
          const tokens = await apiFetch<AuthTokenResponse>("/auth/google", {
            method: "POST",
            body: { idToken: account.id_token },
          });
          const profile = await apiFetch<UserProfile>("/auth/me", { token: tokens.accessToken });
          token.accessToken = tokens.accessToken;
          token.refreshToken = tokens.refreshToken;
          token.accessTokenExpires = Date.now() + tokens.expiresIn * 1000;
          token.roles = profile.roles;
          token.name = profile.displayName;
          token.picture = profile.avatarUrl;
        } catch {
          // Backend từ chối Google token (AUTH_GOOGLE_TOKEN_INVALID) -> không set accessToken,
          // middleware sẽ coi như chưa đăng nhập và điều hướng lại /auth/login.
        }
      }

      // Access token còn hạn -> giữ nguyên, không cần refresh.
      if (token.accessTokenExpires && Date.now() < (token.accessTokenExpires as number)) {
        return token;
      }

      // Hết hạn -> thử refresh (FR-AUTH-004). Nếu fail, xoá accessToken để buộc đăng nhập lại.
      if (token.refreshToken) {
        try {
          const tokens = await apiFetch<AuthTokenResponse>("/auth/refresh", {
            method: "POST",
            body: { refreshToken: token.refreshToken },
          });
          token.accessToken = tokens.accessToken;
          token.refreshToken = tokens.refreshToken;
          token.accessTokenExpires = Date.now() + tokens.expiresIn * 1000;
        } catch {
          token.accessToken = undefined;
        }
      }

      return token;
    },
    async session({ session, token }) {
      // Đưa accessToken + roles ra ngoài session để client component đọc qua useSession().
      session.accessToken = token.accessToken as string | undefined;
      session.roles = (token.roles as string[] | undefined) ?? [];
      if (session.user) session.user.id = token.sub ?? "";
      return session;
    },
  },
});
