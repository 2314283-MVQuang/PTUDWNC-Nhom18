import { Suspense } from "react";
import { AuthShell } from "@/components/layout/AuthShell";
import { ConfirmEmailForm } from "@/components/forms/ConfirmEmailForm";

export const metadata = { title: "Xác nhận email" };

export default function ConfirmEmailPage() {
  return (
    <AuthShell title="Xác nhận email" subtitle="Hoàn tất xác nhận địa chỉ email của tài khoản">
      <Suspense fallback={<div className="skeleton h-40 w-full" />}>
        <ConfirmEmailForm />
      </Suspense>
    </AuthShell>
  );
}
