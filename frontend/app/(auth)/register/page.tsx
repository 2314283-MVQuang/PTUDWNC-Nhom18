import { isDemoAuthEnabled } from "@/lib/auth-mode";
import { Suspense } from "react";
import { AuthShell } from "@/components/layout/AuthShell";
import { AuthCombinedForm } from "@/components/forms/AuthCombinedForm";

export const metadata = { title: "Đăng ký tài khoản" };
export const dynamic = "force-dynamic";

export default function RegisterPage() {
  return (
    <AuthShell
      title="Tài khoản CulinaryBlog"
      subtitle="Đăng ký tài khoản mới hoặc đăng nhập vào hệ thống"
    >
      <Suspense fallback={<div className="skeleton h-56 w-full" />}>
        <AuthCombinedForm initialTab="register" demoEnabled={isDemoAuthEnabled()} googleEnabled={!!(process.env.AUTH_GOOGLE_ID && process.env.AUTH_GOOGLE_SECRET)} />
      </Suspense>
    </AuthShell>
  );
}
