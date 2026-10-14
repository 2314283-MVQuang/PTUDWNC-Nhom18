import type { DefaultSession } from "next-auth";

/**
 * Mở rộng type mặc định của next-auth để thêm accessToken/roles —
 * bắt buộc phải có file này thì TypeScript mới cho phép đọc `session.accessToken`
 * ở component mà không báo lỗi (xem auth.ts, hooks/useAuth.ts).
 */
declare module "next-auth" {
  interface Session extends DefaultSession {
    accessToken?: string;
    isDemo?: boolean;
    roles?: string[];
    user?: DefaultSession["user"] & { id: string };
  }

  interface User {
    roles?: string[];
    accessToken?: string;
    refreshToken?: string;
    accessTokenExpires?: number;
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    accessToken?: string;
    refreshToken?: string;
    accessTokenExpires?: number;
    roles?: string[];
  }
}
