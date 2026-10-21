import { Suspense } from "react";
import { AuthShell } from "@/components/layout/AuthShell";
import { ResetPasswordForm } from "@/components/forms/AccountForms";

export const metadata = { title: "Đặt lại mật khẩu" };

export default function ResetPasswordPage() {
  return (
    <AuthShell title="Đặt lại mật khẩu" subtitle="Nhập mã trong email và chọn mật khẩu mới">
      {/* useSearchParams cần Suspense để Next.js render tĩnh phần còn lại của trang. */}
      <Suspense fallback={<div className="skeleton h-56 w-full" />}>
        <ResetPasswordForm />
      </Suspense>
    </AuthShell>
  );
}
