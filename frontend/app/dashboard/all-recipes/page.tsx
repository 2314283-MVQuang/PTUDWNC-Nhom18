"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Archive, ArchiveRestore, ChevronLeft, ChevronRight, Eye, Library, PenSquare, ShieldAlert, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { ErrorBlock, LoadingBlock } from "@/components/ui/Spinner";
import { StatusBadge } from "@/components/recipe/StatusBadge";
import { useCurrentUser } from "@/hooks/useAuth";
import { useCategories } from "@/hooks/useCategories";
import { useArchiveRecipe, useDeleteRecipe, useRecipes, useUnarchiveRecipe } from "@/hooks/useRecipes";
import { apiErrorMessage } from "@/lib/api-client";
import { cn, formatDate } from "@/lib/utils";
import type { RecipeListParams, RecipeStatus, RecipeSummary } from "@/types/recipe";

const STATUS_OPTIONS = [
  { value: "Published", label: "Đã đăng" },
  { value: "Draft", label: "Bản nháp" },
  { value: "Archived", label: "Đã lưu trữ" },
];
const PAGE_SIZE = 20;

/**
 * "/dashboard/all-recipes" — chỉ Admin: quản lý TẤT CẢ công thức của mọi tác giả (mọi trạng thái).
 *
 * Dùng lại GET /recipes (không gửi mine): với Admin, backend đã trả mọi công thức chưa bị xoá.
 * Lọc theo danh mục và trạng thái (status) phía backend, phân trang 20 dòng.
 * Admin có thể mở form sửa, lưu trữ / khôi phục hoặc xoá công thức của bất kỳ ai (SRS cho phép);
 * backend vẫn tự chặn 403 nếu người không phải Admin gọi thẳng API.
 */
export default function DashboardAllRecipesPage() {
  const { isAdmin, isLoading: sessionLoading } = useCurrentUser();
  const [categoryId, setCategoryId] = useState("");
  const [status, setStatus] = useState<RecipeStatus | "">("");
  const [page, setPage] = useState(1);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => setPage(1), [categoryId, status]);

  const params: RecipeListParams = { page, pageSize: PAGE_SIZE, sort: "-createdAt" };
  if (categoryId) params.categoryId = categoryId;
  if (status) params.status = status;

  const { data, isLoading, isError, error, refetch, isFetching } = useRecipes(params);
  const { data: categories } = useCategories();
  const archiveRecipe = useArchiveRecipe();
  const unarchiveRecipe = useUnarchiveRecipe();
  const deleteRecipe = useDeleteRecipe();
  const isActing = archiveRecipe.isPending || unarchiveRecipe.isPending || deleteRecipe.isPending;

  async function run(action: () => Promise<unknown>, fallback: string) {
    setActionError(null);
    try {
      await action();
    } catch (err) {
      setActionError(apiErrorMessage(err, fallback));
    }
  }

  function archive(recipe: RecipeSummary) {
    if (!confirm(`Lưu trữ "${recipe.title}"? Công thức sẽ ẩn khỏi trang công khai nhưng không bị xoá.`)) return;
    void run(() => archiveRecipe.mutateAsync(recipe.id), "Không lưu trữ được công thức.");
  }

  function unarchive(recipe: RecipeSummary) {
    void run(() => unarchiveRecipe.mutateAsync(recipe.id), "Không khôi phục được công thức.");
  }

  function remove(recipe: RecipeSummary) {
    if (!confirm(`Xoá "${recipe.title}" của ${recipe.authorDisplayName}? Công thức sẽ được ẩn và dữ liệu được dọn sau 30 ngày.`)) return;
    void run(() => deleteRecipe.mutateAsync(recipe.id), "Không xoá được công thức.");
  }

  if (sessionLoading) return <LoadingBlock />;

  if (!isAdmin) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-neutral-200 bg-neutral-50 px-4 py-3 text-sm text-neutral-600">
        <ShieldAlert className="h-4 w-4 text-neutral-500" aria-hidden="true" />
        Chỉ Quản trị viên (Admin) mới quản lý được tất cả công thức. Công thức của bạn nằm ở mục{" "}
        <Link href="/dashboard/recipes" className="font-semibold underline underline-offset-2">Công thức của tôi</Link>.
      </div>
    );
  }

  const meta = data?.meta;
  const items = data?.items ?? [];
  const categoryOptions = (categories ?? []).map((c) => ({ value: c.id, label: c.name }));

  return (
    <div className="flex flex-col gap-6">
      <div>
        <div className="flex items-center gap-2">
          <h1 className="text-2xl font-bold text-neutral-900">Tất cả công thức</h1>
          {meta && (
            <span className="rounded-full border border-brand-200 bg-brand-50 px-2.5 py-0.5 text-xs font-semibold text-brand-700">
              {meta.total} công thức
            </span>
          )}
        </div>
        <p className="mt-1 text-sm text-neutral-500">
          Công thức của mọi tác giả, gồm cả bản nháp và bản lưu trữ. Công thức do chính bạn đăng xem ở mục
          {" "}<Link href="/dashboard/recipes" className="font-semibold text-brand-700 hover:underline">Công thức của tôi</Link>.
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:max-w-xl">
        <Select
          aria-label="Lọc theo danh mục"
          placeholder="Mọi danh mục"
          options={categoryOptions}
          value={categoryId}
          onChange={(e) => setCategoryId(e.target.value)}
        />
        <Select
          aria-label="Lọc theo trạng thái"
          placeholder="Mọi trạng thái"
          options={STATUS_OPTIONS}
          value={status}
          onChange={(e) => setStatus(e.target.value as RecipeStatus | "")}
        />
      </div>

      {actionError && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-4 py-2.5 text-sm text-red-700">
          {actionError}
        </p>
      )}

      {isLoading && <LoadingBlock label="Đang tải danh sách công thức..." />}
      {isError && (
        <ErrorBlock message={apiErrorMessage(error, "Không tải được danh sách công thức.")} onRetry={() => refetch()} />
      )}

      {data && items.length === 0 && (
        <div className="rounded-card border border-dashed border-neutral-200 bg-white py-16 text-center text-sm text-neutral-500">
          <Library className="mx-auto mb-2 h-8 w-8 text-neutral-300" aria-hidden="true" />
          Không có công thức nào phù hợp.
        </div>
      )}

      {items.length > 0 && (
        <div className={cn("overflow-x-auto rounded-card border border-neutral-200 bg-white", isFetching && "opacity-70")}>
          <table className="w-full min-w-[860px] text-left text-sm">
            <thead className="border-b border-neutral-200 bg-neutral-50 text-xs uppercase tracking-wide text-neutral-500">
              <tr>
                <th scope="col" className="px-4 py-3 font-semibold">Tên công thức</th>
                <th scope="col" className="px-4 py-3 font-semibold">Tác giả</th>
                <th scope="col" className="px-4 py-3 font-semibold">Danh mục</th>
                <th scope="col" className="px-4 py-3 font-semibold">Trạng thái</th>
                <th scope="col" className="px-4 py-3 font-semibold">Ngày đăng</th>
                <th scope="col" className="px-4 py-3 text-right font-semibold">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-neutral-100">
              {items.map((recipe) => (
                <tr key={recipe.id}>
                  <td className="px-4 py-3 font-medium text-neutral-900">{recipe.title}</td>
                  <td className="px-4 py-3 text-neutral-600">{recipe.authorDisplayName}</td>
                  <td className="px-4 py-3 text-neutral-600">{recipe.categoryName}</td>
                  <td className="px-4 py-3"><StatusBadge status={recipe.status} /></td>
                  <td className="px-4 py-3 tabular-nums text-neutral-500">{formatDate(recipe.publishedAt) || "—"}</td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-1.5">
                      <Link
                        href={`/recipes/${recipe.slug}`}
                        className="inline-flex h-9 items-center rounded-lg px-2 text-neutral-500 hover:bg-neutral-100 hover:text-neutral-900"
                        aria-label={`Xem ${recipe.title}`}
                        title="Xem"
                      >
                        <Eye className="h-4 w-4" aria-hidden="true" />
                      </Link>
                      <Link
                        href={`/dashboard/recipes/${recipe.slug}/edit`}
                        className="inline-flex h-9 items-center rounded-lg px-2 text-brand-700 hover:bg-brand-50"
                        aria-label={`Sửa ${recipe.title}`}
                        title="Sửa"
                      >
                        <PenSquare className="h-4 w-4" aria-hidden="true" />
                      </Link>
                      {recipe.status === "Archived" ? (
                        <Button size="sm" variant="outline" disabled={isActing} onClick={() => unarchive(recipe)}
                          aria-label={`Khôi phục ${recipe.title}`} title="Khôi phục">
                          <ArchiveRestore className="h-4 w-4" aria-hidden="true" />
                        </Button>
                      ) : (
                        <Button size="sm" variant="outline" disabled={isActing} onClick={() => archive(recipe)}
                          aria-label={`Lưu trữ ${recipe.title}`} title="Lưu trữ">
                          <Archive className="h-4 w-4" aria-hidden="true" />
                        </Button>
                      )}
                      <Button size="sm" variant="danger" disabled={isActing} onClick={() => remove(recipe)}
                        aria-label={`Xoá ${recipe.title}`} title="Xoá">
                        <Trash2 className="h-4 w-4" aria-hidden="true" />
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {meta && meta.totalPages > 1 && (
        <nav className="flex items-center justify-between text-sm text-neutral-600" aria-label="Phân trang công thức">
          <span>Trang {meta.page}/{meta.totalPages}</span>
          <div className="flex gap-2">
            <Button size="sm" variant="outline" disabled={meta.page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
              <ChevronLeft className="mr-1 h-4 w-4" aria-hidden="true" />Trước
            </Button>
            <Button size="sm" variant="outline" disabled={meta.page >= meta.totalPages} onClick={() => setPage((p) => p + 1)}>
              Sau<ChevronRight className="ml-1 h-4 w-4" aria-hidden="true" />
            </Button>
          </div>
        </nav>
      )}
    </div>
  );
}
