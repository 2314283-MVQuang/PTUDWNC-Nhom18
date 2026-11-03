import { auth } from "@/auth";
import { DashboardSidebarNav } from "@/components/layout/DashboardSidebarNav";

export default async function DashboardLayout({ children }: { children: React.ReactNode }) {
  const session = await auth();
  return (
    <div className="container-page grid grid-cols-1 gap-6 py-8 md:grid-cols-[220px_minmax(0,1fr)]">
      <DashboardSidebarNav isAdmin={session?.roles?.includes("Admin") ?? false} />
      <div className="min-w-0">
        {session?.isDemo && (
          <p role="status" className="mb-4 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
            Bạn đang dùng tài khoản demo. Đăng nhập bằng tài khoản chính thức để tạo, sửa hoặc xuất bản công thức.
          </p>
        )}
        {children}
      </div>
    </div>
  );
}
