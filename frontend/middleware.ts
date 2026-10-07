import { auth } from "@/auth";
import { NextResponse } from "next/server";

/**
 * Middleware chạy trước mọi request khớp `matcher` bên dưới.
 * Theo mục 9 (sitemap): /dashboard/** và /profile đều yêu cầu đăng nhập.
 * /dashboard/categories còn yêu cầu role Admin (FR-CAT: Admin-only) — kiểm tra thêm ở đây
 * để tránh Author "lỡ tay" vào được trang quản lý category rồi mới bị chặn ở API.
 */
export default auth((req) => {
  const { pathname } = req.nextUrl;
  const isLoggedIn = !!req.auth?.accessToken;
  const roles = req.auth?.roles ?? [];

  if (!isLoggedIn) {
    const loginUrl = new URL("/login", req.nextUrl.origin);
    loginUrl.searchParams.set("callbackUrl", pathname);
    return NextResponse.redirect(loginUrl);
  }

  if (pathname.startsWith("/dashboard/categories") && !roles.includes("Admin")) {
    return NextResponse.redirect(new URL("/dashboard", req.nextUrl.origin));
  }

  return NextResponse.next();
});

export const config = {
  matcher: ["/dashboard/:path*", "/profile/:path*"],
};
