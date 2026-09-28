"use client";

import { signIn } from "next-auth/react";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { cn } from "@/lib/utils";

interface AuthCombinedFormProps {
  initialTab?: "login" | "register";
}

export function AuthCombinedForm({ initialTab = "login" }: AuthCombinedFormProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const callbackUrl = searchParams.get("callbackUrl") ?? "/dashboard";

  const [tab, setTab] = useState<"login" | "register">(initialTab);

  // State form Đăng nhập
  const [loginAccount, setLoginAccount] = useState(searchParams.get("account") ?? searchParams.get("email") ?? "");
  const [loginPassword, setLoginPassword] = useState("");
  const [loginError, setLoginError] = useState<string | null>(null);
  const [isLoggingIn, setIsLoggingIn] = useState(false);

  // State form Đăng ký (đúng 3 trường: tài khoản, mật khẩu, xác minh lại mật khẩu)
  const [regAccount, setRegAccount] = useState("");
  const [regPassword, setRegPassword] = useState("");
  const [regConfirmPassword, setRegConfirmPassword] = useState("");
  const [regError, setRegError] = useState<string | null>(null);
  const [regSuccess, setRegSuccess] = useState<string | null>(null);
  const [isRegistering, setIsRegistering] = useState(false);

  // Điền nhanh tài khoản mẫu
  function handleQuickFill(acc: string, pass: string) {
    setLoginAccount(acc);
    setLoginPassword(pass);
    setLoginError(null);
  }

  // Xử lý Đăng nhập
  async function handleLoginSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoginError(null);

    if (!loginAccount.trim()) {
      setLoginError("Vui lòng nhập tài khoản hoặc email.");
      return;
    }
    if (!loginPassword) {
      setLoginError("Vui lòng nhập mật khẩu.");
      return;
    }

    setIsLoggingIn(true);
    try {
      const result = await signIn("credentials", {
        account: loginAccount.trim(),
        email: loginAccount.trim(),
        password: loginPassword,
        redirect: false,
      });

      if (result?.error) {
        setLoginError("Tài khoản hoặc mật khẩu không chính xác.");
        setIsLoggingIn(false);
        return;
      }

      router.push(callbackUrl);
      router.refresh();
    } catch {
      setLoginError("Đã có lỗi xảy ra. Vui lòng thử lại.");
      setIsLoggingIn(false);
    }
  }

  // Xử lý Đăng ký
  async function handleRegisterSubmit(e: React.FormEvent) {
    e.preventDefault();
    setRegError(null);
    setRegSuccess(null);

    const acc = regAccount.trim();
    if (!acc) {
      setRegError("Vui lòng nhập tên tài khoản.");
      return;
    }
    if (!regPassword) {
      setRegError("Vui lòng nhập mật khẩu.");
      return;
    }
    if (regPassword !== regConfirmPassword) {
      setRegError("Mật khẩu xác minh không khớp. Vui lòng nhập lại!");
      return;
    }

    setIsRegistering(true);
    try {
      const res = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          account: acc,
          password: regPassword,
          confirmPassword: regConfirmPassword,
        }),
      });

      const data = await res.json();

      if (!res.ok) {
        setRegError(data.error || "Đăng ký thất bại. Vui lòng thử lại!");
        setIsRegistering(false);
        return;
      }

      setRegSuccess(`Đăng ký tài khoản "${acc}" thành công! Đang lưu vào hệ thống...`);

      // Lưu sẵn thông tin sang form Đăng nhập
      setLoginAccount(acc);
      setLoginPassword(regPassword);

      // Tự động đăng nhập và chuyển trang
      setTimeout(async () => {
        const loginRes = await signIn("credentials", {
          account: acc,
          email: acc,
          password: regPassword,
          redirect: false,
        });

        if (!loginRes?.error) {
          router.push(callbackUrl);
          router.refresh();
        } else {
          // Nếu không tự redirect thì chuyển sang tab Đăng nhập để người dùng bấm
          setTab("login");
          setLoginError(null);
          setIsRegistering(false);
        }
      }, 1000);
    } catch {
      setRegError("Không thể kết nối máy chủ. Vui lòng thử lại sau.");
      setIsRegistering(false);
    }
  }

  return (
    <div className="flex flex-col gap-5">
      {/* 2 Tab chuyển đổi Đăng nhập & Đăng ký trên cùng 1 màn hình */}
      <div className="grid grid-cols-2 rounded-xl bg-neutral-100 p-1">
        <button
          type="button"
          onClick={() => {
            setTab("login");
            setLoginError(null);
          }}
          className={cn(
            "rounded-lg py-2.5 text-sm font-semibold transition-all duration-200 cursor-pointer",
            tab === "login"
              ? "bg-white text-brand-800 shadow-sm"
              : "text-neutral-600 hover:text-neutral-900"
          )}
        >
          Đăng nhập
        </button>
        <button
          type="button"
          onClick={() => {
            setTab("register");
            setRegError(null);
          }}
          className={cn(
            "rounded-lg py-2.5 text-sm font-semibold transition-all duration-200 cursor-pointer",
            tab === "register"
              ? "bg-white text-brand-800 shadow-sm"
              : "text-neutral-600 hover:text-neutral-900"
          )}
        >
          Đăng ký tài khoản
        </button>
      </div>

      {/* ======================= TAB ĐĂNG NHẬP ======================= */}
      {tab === "login" && (
        <form onSubmit={handleLoginSubmit} className="flex flex-col gap-4">
          {/* Hộp tài khoản mẫu trong đề bài */}
          <div className="rounded-xl border border-brand-200 bg-brand-50/90 p-3.5 text-xs text-neutral-700 shadow-xs">
            <p className="font-semibold text-brand-900 mb-2 flex items-center gap-1.5">
              <span>🔑</span> Tài khoản mẫu trong đề bài (Bấm để điền nhanh):
            </p>
            <div className="flex flex-col gap-2">
              <button
                type="button"
                onClick={() => handleQuickFill("admin@culinaryblog.local", "Admin@123")}
                className="flex items-center justify-between rounded-lg border border-brand-200 bg-white p-2.5 text-left hover:border-brand-400 hover:bg-brand-50/50 transition-colors shadow-2xs cursor-pointer"
              >
                <div>
                  <div className="font-semibold text-neutral-800">admin@culinaryblog.local</div>
                  <div className="text-[11px] text-neutral-500 font-mono">Mật khẩu: Admin@123 (Quyền: Admin)</div>
                </div>
                <span className="text-[11px] font-semibold text-brand-700 bg-brand-100 px-2.5 py-1 rounded-md">
                  Điền ngay
                </span>
              </button>

              <button
                type="button"
                onClick={() => handleQuickFill("author@culinaryblog.local", "Author@123")}
                className="flex items-center justify-between rounded-lg border border-brand-200 bg-white p-2.5 text-left hover:border-brand-400 hover:bg-brand-50/50 transition-colors shadow-2xs cursor-pointer"
              >
                <div>
                  <div className="font-semibold text-neutral-800">author@culinaryblog.local</div>
                  <div className="text-[11px] text-neutral-500 font-mono">Mật khẩu: Author@123 (Quyền: Author)</div>
                </div>
                <span className="text-[11px] font-semibold text-brand-700 bg-brand-100 px-2.5 py-1 rounded-md">
                  Điền ngay
                </span>
              </button>
            </div>
          </div>

          {loginError && (
            <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
              {loginError}
            </p>
          )}

          {regSuccess && (
            <p role="status" className="rounded-lg bg-green-50 px-3 py-2 text-sm text-green-700">
              {regSuccess}
            </p>
          )}

          <Input
            label="Tài khoản (Tên đăng nhập hoặc Email)"
            type="text"
            placeholder="Ví dụ: 2312 hoặc admin@culinaryblog.local"
            value={loginAccount}
            onChange={(e) => setLoginAccount(e.target.value)}
          />

          <Input
            label="Mật khẩu"
            type="password"
            placeholder="Nhập mật khẩu (Ví dụ: 123)"
            value={loginPassword}
            onChange={(e) => setLoginPassword(e.target.value)}
          />

          <Button type="submit" isLoading={isLoggingIn} className="mt-2 text-base">
            Đăng nhập
          </Button>

          <p className="text-center text-xs text-neutral-500 mt-1">
            Chưa có tài khoản?{" "}
            <button
              type="button"
              onClick={() => setTab("register")}
              className="font-semibold text-brand-700 hover:underline cursor-pointer"
            >
              Đăng ký tài khoản mới
            </button>
          </p>
        </form>
      )}

      {/* ======================= TAB ĐĂNG KÝ ======================= */}
      {tab === "register" && (
        <form onSubmit={handleRegisterSubmit} className="flex flex-col gap-4">
          <div className="rounded-xl border border-blue-200 bg-blue-50/80 p-3 text-xs text-blue-900">
            <p className="font-semibold mb-1">📝 Hướng dẫn đăng ký nhanh:</p>
            <p>Nhập tên tài khoản bất kỳ (vd: <span className="font-mono font-bold">2312</span>), mật khẩu (vd: <span className="font-mono font-bold">123</span>) và xác minh lại mật khẩu. Sau khi đăng ký xong tài khoản sẽ được lưu để bạn đăng nhập.</p>
          </div>

          {regError && (
            <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
              {regError}
            </p>
          )}

          {regSuccess && (
            <p role="status" className="rounded-lg bg-green-50 px-3 py-2 text-sm text-green-700">
              {regSuccess}
            </p>
          )}

          <Input
            label="Tài khoản"
            type="text"
            placeholder="Nhập tên tài khoản (Ví dụ: 2312)"
            value={regAccount}
            onChange={(e) => setRegAccount(e.target.value)}
          />

          <Input
            label="Mật khẩu"
            type="password"
            placeholder="Nhập mật khẩu (Ví dụ: 123)"
            value={regPassword}
            onChange={(e) => setRegPassword(e.target.value)}
          />

          <Input
            label="Xác minh lại mật khẩu"
            type="password"
            placeholder="Nhập lại mật khẩu ở trên"
            value={regConfirmPassword}
            onChange={(e) => setRegConfirmPassword(e.target.value)}
          />

          <Button type="submit" isLoading={isRegistering} className="mt-2 text-base">
            Xác nhận Đăng ký
          </Button>

          <p className="text-center text-xs text-neutral-500 mt-1">
            Đã có tài khoản?{" "}
            <button
              type="button"
              onClick={() => setTab("login")}
              className="font-semibold text-brand-700 hover:underline cursor-pointer"
            >
              Quay lại Đăng nhập
            </button>
          </p>
        </form>
      )}
    </div>
  );
}
