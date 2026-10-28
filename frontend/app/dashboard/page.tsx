"use client";

import { useState } from "react";
import Link from "next/link";
import {
  BookOpen,
  ChevronDown,
  ChevronUp,
  Clock,
  ExternalLink,
  FilePlus2,
  FolderTree,
  PenSquare,
} from "lucide-react";
import { Button } from "@/components/ui/Button";
import { LoadingBlock, ErrorBlock } from "@/components/ui/Spinner";
import { StatusBadge } from "@/components/recipe/StatusBadge";
import { useCurrentUser } from "@/hooks/useAuth";
import { useRecipes } from "@/hooks/useRecipes";
import { useCategories } from "@/hooks/useCategories";
import { formatDate, formatMinutes } from "@/lib/utils";
import type { RecipeStatus, RecipeSummary } from "@/types/recipe";

/**
 * Mục 9: "/dashboard" — CSR, trang tổng quan sau khi đăng nhập.
 * Thẻ trạng thái có thể nhấn để xổ danh sách công thức.
 * Hiển thị dải danh mục trực tiếp để xem nhanh.
 */
export default function DashboardOverviewPage() {
  const { user, isAdmin } = useCurrentUser();
  const { data, isLoading, isError, refetch } = useRecipes({
    pageSize: 50,
    sort: "-createdAt",
  });
  const { data: categories } = useCategories();

  // Trạng thái đang mở (null = đóng hết)
  const [openStatus, setOpenStatus] = useState<RecipeStatus | null>(null);

  const recipes = data?.items ?? [];
  const byStatus = (status: RecipeStatus) => recipes.filter((r) => r.status === status);

  const statusGroups: { status: RecipeStatus; label: string; color: string }[] = [
    { status: "Draft", label: "Bản nháp", color: "text-neutral-500 border-neutral-200 bg-neutral-50" },
    { status: "Published", label: "Đã đăng", color: "text-herb-700 border-herb-200 bg-herb-50" },
    { status: "Archived", label: "Đã lưu trữ", color: "text-amber-700 border-amber-200 bg-amber-50" },
  ];

  function toggleStatus(status: RecipeStatus) {
    setOpenStatus((prev) => (prev === status ? null : status));
  }

  return (
    <div className="flex flex-col gap-8">
      <div>
        <h1 className="text-2xl font-bold text-neutral-900">Xin chào, {user?.name ?? "bạn"} 👋</h1>
        <p className="mt-1 text-neutral-500">
          {isAdmin ? "Bạn đang đăng nhập với quyền Admin." : "Theo dõi các công thức và danh mục bạn được phép xem."}
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
        <Link href="/dashboard/categories">
          <Button variant="outline">
            <FolderTree className="mr-2 h-4 w-4" />
            {isAdmin ? "Quản lý danh mục" : "Xem danh mục"}
          </Button>
        </Link>
      </div>

      {isLoading && <LoadingBlock label="Đang tải thống kê..." />}
      {isError && <ErrorBlock message="Không tải được dữ liệu công thức." onRetry={() => refetch()} />}

      {!isLoading && !isError && (
        <>
          {/* Section: Thống kê trạng thái công thức */}
          <div className="flex flex-col gap-3">
            <div className="flex items-center justify-between">
              <h2 className="text-base font-bold text-neutral-900">Trạng thái công thức</h2>
              <span className="text-xs text-neutral-500">Nhấn vào từng mục để xem danh sách</span>
            </div>

            <div className="flex flex-col gap-3">
              {statusGroups.map(({ status, label, color }) => {
                const list = byStatus(status);
                const isOpen = openStatus === status;
                return (
                  <div key={status} className={`rounded-card border ${color} overflow-hidden`}>
                    {/* Header nhấn được */}
                    <button
                      type="button"
                      onClick={() => toggleStatus(status)}
                      className="flex w-full items-center justify-between px-5 py-4 text-left transition-colors hover:opacity-80"
                    >
                      <div className="flex items-center gap-4">
                        <span className="text-3xl font-bold">{list.length}</span>
                        <span className="text-sm font-semibold">{label}</span>
                      </div>
                      <div className="flex items-center gap-2 text-xs opacity-60">
                        {list.length > 0 && (
                          <span>{isOpen ? "Thu gọn" : `Xem ${list.length} công thức`}</span>
                        )}
                        {isOpen ? (
                          <ChevronUp className="h-4 w-4" />
                        ) : (
                          <ChevronDown className="h-4 w-4" />
                        )}
                      </div>
                    </button>

                    {/* Danh sách công thức xổ xuống */}
                    {isOpen && (
                      <div className="border-t border-current/10 bg-white">
                        {list.length === 0 ? (
                          <p className="px-5 py-4 text-sm text-neutral-400">Không có công thức nào.</p>
                        ) : (
                          <ul className="divide-y divide-neutral-100">
                            {list.map((recipe) => (
                              <RecipeListItem key={recipe.id} recipe={recipe} />
                            ))}
                          </ul>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          {/* Section: Danh mục món ăn hiện rõ trên trang */}
          {categories && categories.length > 0 && (
            <div className="rounded-card border border-neutral-200 bg-white p-6 shadow-soft">
              <div className="mb-4 flex items-center justify-between">
                <div>
                  <h2 className="text-base font-bold text-neutral-900 flex items-center gap-2">
                    <FolderTree className="h-5 w-5 text-brand-600" />
                    Danh mục món ăn ({categories.length})
                  </h2>
                  <p className="mt-0.5 text-xs text-neutral-500">
                    Bấm vào từng danh mục để xem nhanh các công thức thuộc nhóm đó
                  </p>
                </div>
                <Link
                  href="/dashboard/categories"
                  className="text-xs font-semibold text-brand-700 hover:underline flex items-center gap-1"
                >
                  Xem bảng chi tiết
                  <ExternalLink className="h-3 w-3" />
                </Link>
              </div>

              <div className="flex flex-wrap gap-2">
                {categories.map((cat) => (
                  <Link
                    key={cat.id}
                    href={`/categories/${cat.slug}`}
                    target="_blank"
                    className="group inline-flex items-center gap-1.5 rounded-full border border-neutral-200 bg-neutral-50 px-3.5 py-1.5 text-xs font-medium text-neutral-700 transition-all hover:border-brand-300 hover:bg-brand-50 hover:text-brand-800"
                  >
                    <span>{cat.name}</span>
                    <span className="rounded-full bg-white px-1.5 py-0.5 text-[10px] font-bold text-neutral-500 group-hover:text-brand-600 shadow-2xs">
                      {cat.recipeCount}
                    </span>
                  </Link>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

/** Một dòng công thức trong danh sách xổ xuống */
function RecipeListItem({ recipe }: { recipe: RecipeSummary }) {
  const totalTime = recipe.prepTime + recipe.cookTime;
  return (
    <li className="flex items-center justify-between gap-4 px-5 py-3 hover:bg-neutral-50">
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium text-neutral-900">{recipe.title}</p>
        <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-neutral-500">
          <span>{recipe.categoryName}</span>
          {totalTime > 0 && (
            <span className="flex items-center gap-1">
              <Clock className="h-3 w-3" />
              {formatMinutes(totalTime)}
            </span>
          )}
          {recipe.publishedAt && <span>{formatDate(recipe.publishedAt)}</span>}
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <StatusBadge status={recipe.status} />
        <Link
          href={`/dashboard/recipes/${recipe.slug}/edit`}
          className="inline-flex items-center gap-1 rounded-lg px-2.5 py-1.5 text-xs font-medium text-brand-700 hover:bg-brand-50"
          title="Chỉnh sửa"
        >
          <PenSquare className="h-3.5 w-3.5" />
          Sửa
        </Link>
        {recipe.status === "Published" && (
          <Link
            href={`/recipes/${recipe.slug}`}
            target="_blank"
            className="inline-flex items-center gap-1 rounded-lg px-2.5 py-1.5 text-xs font-medium text-neutral-600 hover:bg-neutral-100"
            title="Xem trang công khai"
          >
            <ExternalLink className="h-3.5 w-3.5" />
            Xem
          </Link>
        )}
      </div>
    </li>
  );
}
