"use client";

import { Suspense } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { FilePlus2, PenSquare, BookOpen } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { Pagination } from "@/components/ui/Pagination";
import { ErrorBlock, LoadingBlock } from "@/components/ui/Spinner";
import { StatusBadge } from "@/components/recipe/StatusBadge";
import { useRecipes } from "@/hooks/useRecipes";
import { useCurrentUser } from "@/hooks/useAuth";
import { apiErrorMessage } from "@/lib/api-client";
import { formatDate } from "@/lib/utils";
import type { RecipeStatus } from "@/types/recipe";

export default function RecipeWorkspace() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <RecipesWorkspaceContent />
    </Suspense>
  );
}

function RecipesWorkspaceContent() {
  const params = useSearchParams();
  const router = useRouter();
  const { isAdmin } = useCurrentUser();
  const page = Math.max(1, Number(params.get("page")) || 1);
  const status = params.get("status") as RecipeStatus | null;
  const mine = !isAdmin || params.get("mine") === "true";
  const { data, isLoading, isError, error, refetch } = useRecipes({
    page,
    pageSize: 12,
    sort: "-createdAt",
    mine,
    status: status || undefined,
  });
  const href = (changes: Record<string, string>) => {
    const query = new URLSearchParams(params.toString());
    for (const [key, value] of Object.entries(changes)) {
      if (value) query.set(key, value);
      else query.delete(key);
    }
    return `/dashboard/recipes?${query}`;
  };
  return (
    <section className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-5 rounded-card-lg border border-brand-100 bg-gradient-to-r from-brand-50 to-white p-6">
        <div>
          <p className="mb-2 flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-brand-700">
            <BookOpen className="h-4 w-4" />
            Sổ tay nấu ăn
          </p>
          <h1 className="text-2xl font-bold">
            {mine ? "Công thức của tôi" : "Quản lý công thức"}
          </h1>
          <p className="mt-2 max-w-lg text-sm text-neutral-600">
            Viết bản nháp, thêm ảnh và chia sẻ món ăn khi công thức đã sẵn sàng.
          </p>
        </div>
        <Link href="/dashboard/recipes/new">
          <Button>
            <FilePlus2 className="h-4 w-4" />
            Tạo công thức mới
          </Button>
        </Link>
      </div>
      <div className="flex flex-wrap items-end gap-4 rounded-card border border-neutral-200 bg-white p-4">
        <Select
          name="recipe-status"
          label="Trạng thái"
          value={status ?? ""}
          options={[
            { value: "", label: "Tất cả trạng thái" },
            { value: "Draft", label: "Bản nháp" },
            { value: "Published", label: "Đã xuất bản" },
            { value: "Archived", label: "Đã lưu trữ" },
          ]}
          onChange={(event) =>
            router.push(href({ status: event.target.value, page: "1" }))
          }
        />
        {isAdmin && (
          <Select
            name="recipe-scope"
            label="Phạm vi"
            value={mine ? "true" : "false"}
            options={[
              { value: "false", label: "Tất cả công thức" },
              { value: "true", label: "Công thức của tôi" },
            ]}
            onChange={(event) =>
              router.push(href({ mine: event.target.value, page: "1" }))
            }
          />
        )}
        {data && (
          <p className="pb-2 text-sm text-neutral-500">
            {data.meta.total} công thức
          </p>
        )}
      </div>
      {isLoading && <LoadingBlock />}
      {isError && (
        <ErrorBlock
          message={apiErrorMessage(
            error,
            "Không tải được danh sách công thức.",
          )}
          onRetry={() => refetch()}
        />
      )}
      {data && (
        <>
          {data.items.length === 0 ? (
            <div className="rounded-card-lg border border-dashed border-neutral-300 bg-white p-12 text-center">
              <BookOpen className="mx-auto mb-3 h-8 w-8 text-brand-400" />
              <h2 className="font-semibold">Chưa có công thức phù hợp</h2>
              <p className="mt-2 text-sm text-neutral-500">
                Tạo công thức mới hoặc chọn trạng thái khác để xem.
              </p>
            </div>
          ) : (
            <div className="overflow-x-auto rounded-card-lg border border-neutral-200 bg-white">
              <table className="w-full text-left text-sm">
                <thead className="bg-neutral-50 text-neutral-500">
                  <tr>
                    {[
                      "Công thức",
                      "Danh mục",
                      "Trạng thái",
                      "Ngày xuất bản",
                      "",
                    ].map((label, index) => (
                      <th key={index} className="px-4 py-3 font-medium">
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-neutral-100">
                  {data.items.map((recipe) => (
                    <tr key={recipe.id} className="hover:bg-brand-50/40">
                      <td className="min-w-48 px-4 py-5 font-semibold">
                        {recipe.title}
                      </td>
                      <td className="px-4 py-5 text-neutral-500">
                        {recipe.categoryName}
                      </td>
                      <td className="px-4 py-5">
                        <StatusBadge status={recipe.status} />
                      </td>
                      <td className="whitespace-nowrap px-4 py-5 text-neutral-500">
                        {formatDate(recipe.publishedAt) || "—"}
                      </td>
                      <td className="whitespace-nowrap px-4 py-5">
                        <Link
                          href={`/dashboard/recipes/${recipe.slug}/edit`}
                          className="inline-flex items-center gap-1 font-semibold text-brand-700 hover:underline"
                        >
                          <PenSquare className="h-4 w-4" />
                          Chỉnh sửa
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <Pagination
            meta={data.meta}
            buildHref={(number) => href({ page: String(number) })}
          />
        </>
      )}
    </section>
  );
}
