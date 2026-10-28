import { auth } from "@/auth";
import { DashboardSidebarNav } from "@/components/layout/DashboardSidebarNav";

/**
 * Layout cho toàn bộ /dashboard/** — sidebar điều hướng.
 */
export default async function DashboardLayout({ children }: { children: React.ReactNode }) {
  const session = await auth();
  return (
    <div className="container-page grid grid-cols-1 gap-8 py-10 md:grid-cols-[220px_1fr]">
      <DashboardSidebarNav />
      <div>
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
