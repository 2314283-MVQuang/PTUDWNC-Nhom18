"use client";

import Link from "next/link";
import { BookOpen, FilePlus2, PenLine } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { LoadingBlock, ErrorBlock } from "@/components/ui/Spinner";
import { useCurrentUser } from "@/hooks/useAuth";
import { useRecipes } from "@/hooks/useRecipes";
import type { RecipeStatus } from "@/types/recipe";

/**
 * Mục 9: "/dashboard" — CSR, trang tổng quan sau khi đăng nhập.
 * Chỉ là 1 widget thống kê nhỏ (đếm công thức theo trạng thái) + link nhanh, không phải
 * dashboard đầy đủ — nếu cần số liệu chính xác/nhiều hơn pageSize, nên làm 1 endpoint
 * thống kê riêng ở backend thay vì đếm bằng tay ở FE như dưới đây.
 */
export default function DashboardOverviewPage() {
  const { user, isAdmin } = useCurrentUser();
  const { data, isLoading, isError, refetch } = useRecipes({
    pageSize: 50,
    sort: "-createdAt",
  });

  // Thống kê trên trang đầu của danh sách được phép xem, không phải tổng toàn hệ thống.
  const recipes = data?.items ?? [];
  const countByStatus = (status: RecipeStatus) => recipes.filter((r) => r.status === status).length;

  return (
    <div className="flex flex-col gap-8">
      <div>
        <h1 className="text-2xl font-bold text-neutral-900">Xin chào, {user?.name ?? "bạn"} 👋</h1>
        <p className="mt-1 text-neutral-500">
          {isAdmin ? "Bạn đang đăng nhập với quyền Admin." : "Theo dõi các công thức bạn được phép xem."}
        </p>
      </div>

      <div className="flex flex-wrap gap-3">
        <Link href="/dashboard/recipes/new">
          <Button>
            <FilePlus2 className="mr-2 h-4 w-4" />
            Tạo công thức mới
          </Button>
        </Link>
        <Link href="/dashboard/recipes">
          <Button variant="outline">
            <BookOpen className="mr-2 h-4 w-4" />
            Quản lý công thức
          </Button>
        </Link>
        {isAdmin && (
          <Link href="/dashboard/categories">
            <Button variant="outline">
              <PenLine className="mr-2 h-4 w-4" />
              Quản lý danh mục
            </Button>
          </Link>
        )}
      </div>

      {isLoading && <LoadingBlock label="Đang tải thống kê..." />}
      {isError && <ErrorBlock message="Không tải được dữ liệu công thức." onRetry={() => refetch()} />}

      {!isLoading && !isError && (
        <p className="text-sm text-neutral-500">Thống kê trên tối đa 50 công thức trong danh sách hiện tại.</p>
      )}
      {!isLoading && !isError && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <StatCard label="Bản nháp" value={countByStatus("Draft")} />
          <StatCard label="Đã đăng" value={countByStatus("Published")} />
          <StatCard label="Đã lưu trữ" value={countByStatus("Archived")} />
        </div>
      )}
    </div>
  );
}

function StatCard({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-card border border-neutral-200 bg-white p-6">
      <p className="text-3xl font-bold text-neutral-900">{value}</p>
      <p className="mt-1 text-sm text-neutral-500">{label}</p>
    </div>
  );
}
