import { AuthShell } from "@/components/layout/AuthShell";
import { PasswordForm } from "@/components/forms/PasswordForm";
import type { Metadata } from "next";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Đặt lại mật khẩu", robots: { index: false }, referrer: "no-referrer" };

export default async function ResetPasswordPage({ searchParams }: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  return <AuthShell title="Đặt lại mật khẩu" subtitle="Chọn mật khẩu mới cho tài khoản.">
    <PasswordForm mode="reset" email={typeof params.email === "string" ? params.email : ""}
      token={typeof params.token === "string" ? params.token : ""} />
  </AuthShell>;
}
