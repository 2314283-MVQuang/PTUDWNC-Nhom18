import { isDemoAuthEnabled } from "@/lib/auth-mode";
import { Suspense } from "react";
import { AuthShell } from "@/components/layout/AuthShell";
import { AuthCombinedForm } from "@/components/forms/AuthCombinedForm";

export const metadata = { title: "Đăng nhập / Đăng ký" };
export const dynamic = "force-dynamic";

export default function LoginPage() {
  return (
    <AuthShell
      title="Tài khoản CulinaryBlog"
      subtitle="Đăng nhập hoặc đăng ký tài khoản để khám phá và quản lý công thức"
    >
      <Suspense fallback={<div className="skeleton h-56 w-full" />}>
        <AuthCombinedForm initialTab="login" demoEnabled={isDemoAuthEnabled()} googleEnabled={!!(process.env.AUTH_GOOGLE_ID && process.env.AUTH_GOOGLE_SECRET)} />
      </Suspense>
    </AuthShell>
  );
}
