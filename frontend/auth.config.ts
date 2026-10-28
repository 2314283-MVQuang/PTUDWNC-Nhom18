import type { NextAuthConfig } from "next-auth";
import { apiFetch, ApiError } from "@/lib/api-client";
import type { AuthTokenResponse, UserProfile } from "@/types/user";
import { isDemoAuthEnabled, isLocalAccessToken } from "@/lib/auth-mode";

// Không import Node fs tại đây: middleware Edge dùng chung cấu hình này.
export const authConfig = {
  pages: { signIn: "/login" },
  session: { strategy: "jwt" },
  providers: [],
  callbacks: {
    async jwt({ token, user, account, trigger }) {
      if (isLocalAccessToken(typeof token.accessToken === "string" ? token.accessToken : undefined) && !isDemoAuthEnabled()) {
        token.accessToken = undefined;
        token.refreshToken = undefined;
        token.roles = [];
        return token;
      }
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
          token.sub = profile.id;
          token.name = profile.displayName;
          token.picture = profile.avatarUrl;
        } catch {
          // Backend từ chối Google token (AUTH_GOOGLE_TOKEN_INVALID) -> không set accessToken,
          // middleware sẽ coi như chưa đăng nhập và điều hướng lại /auth/login.
        }
      }

      // Hết hạn -> thử refresh (FR-AUTH-004). Nếu fail, xoá accessToken để buộc đăng nhập lại.
      if (typeof token.refreshToken === "string" && (typeof token.accessTokenExpires !== "number" || Date.now() >= token.accessTokenExpires)) {
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
          token.refreshToken = undefined;
          token.roles = [];
        }
      }

      // update() chỉ là tín hiệu nạp lại. Không tin tên/role/token do trình duyệt gửi lên.
      if (trigger === "update" && typeof token.accessToken === "string" && !isLocalAccessToken(token.accessToken)) {
        try {
          const profile = await apiFetch<UserProfile>("/auth/me", { token: token.accessToken, cache: "no-store" });
          token.sub = profile.id;
          token.name = profile.displayName;
          token.email = profile.email;
          token.picture = profile.avatarUrl;
          token.roles = profile.roles;
        } catch (error) {
          // Mất mạng khi đồng bộ hồ sơ không được làm Auth.js xóa cookie đang hợp lệ.
          if (error instanceof ApiError && error.status === 401) {
            token.accessToken = undefined; token.refreshToken = undefined; token.roles = [];
          }
        }
      }

      return token;
    },
    async session({ session, token }) {
      // Đưa accessToken + roles ra ngoài session để client component đọc qua useSession().
      const accessToken = typeof token.accessToken === "string" ? token.accessToken : undefined;
      session.accessToken = isLocalAccessToken(accessToken) && !isDemoAuthEnabled()
        ? undefined : accessToken;
      session.isDemo = isDemoAuthEnabled() && isLocalAccessToken(session.accessToken);
      session.roles = (token.roles as string[] | undefined) ?? [];
      if (session.user) {
        session.user.id = token.sub ?? "";
        session.user.name = token.name ?? "";
        session.user.email = token.email ?? "";
        session.user.image = token.picture ?? null;
      }
      return session;
    },
  },
} satisfies NextAuthConfig;
