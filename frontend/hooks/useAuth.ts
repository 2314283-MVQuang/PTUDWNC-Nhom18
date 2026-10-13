"use client";

import { useSession } from "next-auth/react";

/**
 * Hook tiện ích bọc quanh `useSession()` của Auth.js — mọi component cần biết
 * "ai đang đăng nhập / có quyền gì" nên dùng hook này thay vì đọc thẳng useSession()
 * để không phải lặp lại logic kiểm tra `status === "loading"` ở mọi nơi.
 */
export function useCurrentUser() {
  const { data: session, status } = useSession();

  return {
    user: session?.user,
    roles: session?.roles ?? [],
    accessToken: session?.isDemo ? undefined : session?.accessToken,
    isDemo: session?.isDemo ?? false,
    isAuthenticated: status === "authenticated" && !!session?.accessToken,
    isLoading: status === "loading",
    isAdmin: (session?.roles ?? []).includes("Admin"),
  };
}
