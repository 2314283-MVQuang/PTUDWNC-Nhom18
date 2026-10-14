"use client";

import Link from "next/link";
import { FilePlus2, PenSquare } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { ErrorBlock, LoadingBlock } from "@/components/ui/Spinner";
import { StatusBadge } from "@/components/recipe/StatusBadge";
import { useRecipes } from "@/hooks/useRecipes";
import { formatDate } from "@/lib/utils";

/**
 * Mục 9: "/dashboard/recipes" — CSR, danh sách công thức để vào trang chỉnh sửa.
 *
 * GET /recipes trả Published cùng bản nháp/lưu trữ của chủ sở hữu; Admin xem tất cả.
 * Danh sách không có bộ lọc "chỉ của tôi", vì vậy tiêu đề và thông báo phản ánh đúng phạm vi.
 */
export default function DashboardRecipesPage() {
  const { data, isLoading, isError, refetch } = useRecipes({
    pageSize: 50,
    sort: "-createdAt",
  });

  const recipes = data?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-neutral-900">Danh sách công thức</h1>
        <Link href="/dashboard/recipes/new">
          <Button>
            <FilePlus2 className="mr-2 h-4 w-4" />
            Tạo công thức mới
          </Button>
        </Link>
      </div>

      <p className="rounded-card border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
        Danh sách gồm công thức đã xuất bản và bản nháp/lưu trữ của bạn; quản trị viên xem
        được tất cả. Bạn có thể chỉnh sửa công thức do mình tạo hoặc bằng quyền quản trị.
      </p>

      {isLoading && <LoadingBlock />}
      {isError && <ErrorBlock message="Không tải được danh sách công thức." onRetry={() => refetch()} />}

      {!isLoading && !isError && recipes.length === 0 && (
        <div className="rounded-card border border-dashed border-neutral-300 py-16 text-center text-neutral-500">
          Chưa có công thức nào trong danh sách. Bấm &quot;Tạo công thức mới&quot; để bắt đầu.
        </div>
      )}

      {!isLoading && !isError && recipes.length > 0 && (
        <div className="overflow-x-auto rounded-card border border-neutral-200">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-neutral-200 bg-neutral-50 text-neutral-500">
              <tr>
                <th className="px-4 py-3 font-medium">Tên công thức</th>
                <th className="px-4 py-3 font-medium">Danh mục</th>
                <th className="px-4 py-3 font-medium">Trạng thái</th>
                <th className="px-4 py-3 font-medium">Ngày đăng</th>
                <th className="px-4 py-3 font-medium" />
              </tr>
            </thead>
            <tbody className="divide-y divide-neutral-100">
              {recipes.map((recipe) => (
                <tr key={recipe.id}>
                  <td className="px-4 py-3 font-medium text-neutral-900">{recipe.title}</td>
                  <td className="px-4 py-3 text-neutral-600">{recipe.categoryName}</td>
                  <td className="px-4 py-3">
                    <StatusBadge status={recipe.status} />
                  </td>
                  <td className="px-4 py-3 text-neutral-500">{formatDate(recipe.publishedAt) || "—"}</td>
                  <td className="px-4 py-3 text-right">
                    <Link
                      href={`/dashboard/recipes/${recipe.slug}/edit`}
                      className="inline-flex items-center gap-1 text-sm font-medium text-brand-700 hover:underline"
                    >
                      <PenSquare className="h-4 w-4" />
                      Mở công thức
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
