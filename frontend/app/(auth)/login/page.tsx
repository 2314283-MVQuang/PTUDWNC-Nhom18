import { Suspense } from "react";
import { AuthShell } from "@/components/layout/AuthShell";
import { AuthCombinedForm } from "@/components/forms/AuthCombinedForm";

export const metadata = { title: "Đăng nhập / Đăng ký" };

export default function LoginPage() {
  return (
    <AuthShell
      title="Tài khoản CulinaryBlog"
      subtitle="Đăng nhập hoặc đăng ký tài khoản để khám phá và quản lý công thức"
    >
      <Suspense fallback={<div className="skeleton h-56 w-full" />}>
        <AuthCombinedForm initialTab="login" />
      </Suspense>
    </AuthShell>
  );
}
