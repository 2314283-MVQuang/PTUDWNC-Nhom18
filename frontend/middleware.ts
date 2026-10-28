import NextAuth from "next-auth";
import { authConfig } from "@/auth.config";
import { NextResponse } from "next/server";

const { auth } = NextAuth(authConfig);

/**
 * Middleware chạy trước mọi request khớp `matcher` bên dưới.
 * Theo mục 9 (sitemap): /dashboard/** và /profile đều yêu cầu đăng nhập.
 */
export default auth((req) => {
  const { pathname } = req.nextUrl;
  const isLoggedIn = !!req.auth?.accessToken;

  if (!isLoggedIn) {
    const loginUrl = new URL("/login", req.nextUrl.origin);
    loginUrl.searchParams.set("callbackUrl", pathname);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
});

export const config = {
  matcher: ["/dashboard/:path*", "/profile/:path*"],
};
