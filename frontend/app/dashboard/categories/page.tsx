"use client";

import { useState, useMemo } from "react";
import Image from "next/image";
import Link from "next/link";
import {
  ExternalLink,
  FolderTree,
  LayoutGrid,
  List,
  Pencil,
  Plus,
  Search,
  ShieldAlert,
  Trash2,
} from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Card } from "@/components/ui/Card";
import { ErrorBlock, LoadingBlock } from "@/components/ui/Spinner";
import { CategoryForm } from "@/components/forms/CategoryForm";
import {
  useCategories,
  useCreateCategory,
  useDeleteCategory,
  useUpdateCategory,
} from "@/hooks/useCategories";
import { useCurrentUser } from "@/hooks/useAuth";
import { ApiError } from "@/lib/api-client";
import type { CategoryFormValues } from "@/lib/validations/recipe.schema";
import type { Category } from "@/types/category";

const CATEGORY_THEME: Array<{
  pattern: RegExp;
  emoji: string;
  bg: string;
  badge: string;
}> = [
  { pattern: /khai vị|salad|gỏi/i,       emoji: "🥗", bg: "from-green-50 to-emerald-100", badge: "bg-emerald-100 text-emerald-800" },
  { pattern: /tráng miệng|ngọt|chè/i,    emoji: "🍮", bg: "from-pink-50 to-rose-100",    badge: "bg-rose-100 text-rose-800" },
  { pattern: /bánh/i,                    emoji: "🍰", bg: "from-amber-50 to-yellow-100", badge: "bg-yellow-100 text-yellow-800" },
  { pattern: /súp|canh/i,                emoji: "🍵", bg: "from-sky-50 to-blue-100",     badge: "bg-blue-100 text-blue-800" },
  { pattern: /lẩu|nồi/i,                 emoji: "🍲", bg: "from-orange-50 to-amber-100",  badge: "bg-amber-100 text-amber-800" },
  { pattern: /mì|phở|bún|noodle|miến/i, emoji: "🍜", bg: "from-yellow-50 to-amber-100",  badge: "bg-amber-100 text-amber-800" },
  { pattern: /nướng|bbq|rang/i,          emoji: "🍖", bg: "from-red-50 to-orange-100",   badge: "bg-orange-100 text-orange-800" },
  { pattern: /hải sản|cá|tôm|mực/i,     emoji: "🦐", bg: "from-cyan-50 to-sky-100",    badge: "bg-sky-100 text-sky-800" },
  { pattern: /chay/i,                    emoji: "🥬", bg: "from-lime-50 to-green-100",  badge: "bg-green-100 text-green-800" },
  { pattern: /đồ uống|nước|sinh tố/i,   emoji: "🥤", bg: "from-violet-50 to-purple-100", badge: "bg-purple-100 text-purple-800" },
  { pattern: /sáng|breakfast/i,          emoji: "🍳", bg: "from-yellow-50 to-orange-100", badge: "bg-orange-100 text-orange-800" },
  { pattern: /chiên|xào/i,              emoji: "🥘", bg: "from-orange-50 to-red-100",    badge: "bg-red-100 text-red-800" },
  { pattern: /hấp/i,                     emoji: "♨️", bg: "from-teal-50 to-cyan-100",    badge: "bg-teal-100 text-teal-800" },
  { pattern: /kho/i,                     emoji: "🍛", bg: "from-amber-50 to-orange-100", badge: "bg-orange-100 text-orange-800" },
  { pattern: /Nhật|Hàn|Tàu|Ý|Tây/i,      emoji: "🌍", bg: "from-indigo-50 to-blue-100",  badge: "bg-indigo-100 text-indigo-800" },
  { pattern: /eat clean|healthy/i,       emoji: "🥑", bg: "from-green-50 to-lime-100",   badge: "bg-lime-100 text-lime-800" },
];

function pickTheme(name: string) {
  for (const theme of CATEGORY_THEME) {
    if (theme.pattern.test(name)) return theme;
  }
  return { emoji: "🍽️", bg: "from-brand-50 to-orange-100", badge: "bg-orange-100 text-orange-800" };
}

/**
 * Mục 9: "/dashboard/categories" — Quản lý & xem danh mục trong khu vực quản trị.
 * Mặc định hiển thị dạng lưới thẻ trực quan (Card Grid) kèm icon và màu sắc như trang công khai.
 */
export default function DashboardCategoriesPage() {
  const { isAdmin } = useCurrentUser();
  const { data: categories, isLoading, isError, refetch } = useCategories();
  const createCategory = useCreateCategory();
  const updateCategory = useUpdateCategory();
  const deleteCategory = useDeleteCategory();

  const [viewMode, setViewMode] = useState<"grid" | "table">("grid");
  const [search, setSearch] = useState("");
  const [mode, setMode] = useState<"none" | "create" | Category>("none");
  const [deleteErrorId, setDeleteErrorId] = useState<string | null>(null);

  const filteredCategories = useMemo(() => {
    if (!categories) return [];
    if (!search.trim()) return categories;
    const q = search.trim().toLowerCase();
    return categories.filter(
      (c) => c.name.toLowerCase().includes(q) || c.slug.toLowerCase().includes(q)
    );
  }, [categories, search]);

  async function handleCreate(values: CategoryFormValues) {
    await createCategory.mutateAsync(values);
    setMode("none");
  }

  async function handleUpdate(id: string, values: CategoryFormValues) {
    await updateCategory.mutateAsync({ id, input: values });
    setMode("none");
  }

  async function handleDelete(category: Category) {
    if (!confirm(`Xoá danh mục "${category.name}"?`)) return;
    setDeleteErrorId(null);
    try {
      await deleteCategory.mutateAsync(category.id);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setDeleteErrorId(category.id);
      }
    }
  }

  return (
    <div className="flex flex-col gap-6">
      {/* Tiêu đề trang + nút thêm */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-bold text-neutral-900">Danh mục món ăn</h1>
            <span className="rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-semibold text-brand-700 border border-brand-200">
              {categories ? `${categories.length} danh mục` : ""}
            </span>
          </div>
          <p className="mt-1 text-sm text-neutral-500">
            Hiển thị rõ ràng các nhóm món ăn, số lượng công thức và thao tác quản lý.
          </p>
        </div>

        {isAdmin && mode === "none" && (
          <Button onClick={() => setMode("create")}>
            <Plus className="mr-2 h-4 w-4" />
            Thêm danh mục mới
          </Button>
        )}
      </div>

      {!isAdmin && (
        <div className="flex items-center gap-2 rounded-lg border border-neutral-200 bg-neutral-50 px-4 py-2.5 text-xs text-neutral-600">
          <ShieldAlert className="h-4 w-4 text-neutral-500" />
          <span>Chế độ xem: Bạn đang xem danh mục với vai trò Tác giả. Quản trị viên (Admin) mới có quyền thêm/sửa/xoá.</span>
        </div>
      )}

      {/* Form thêm mới */}
      {isAdmin && mode === "create" && (
        <div className="rounded-card border border-brand-200 bg-brand-50/40 p-6 shadow-sm">
          <h2 className="mb-4 font-semibold text-neutral-900">Thêm danh mục mới</h2>
          <CategoryForm onSubmit={handleCreate} onCancel={() => setMode("none")} isSubmitting={createCategory.isPending} />
        </div>
      )}

      {/* Form chỉnh sửa */}
      {isAdmin && mode !== "none" && mode !== "create" && (
        <div className="rounded-card border border-amber-200 bg-amber-50/40 p-6 shadow-sm">
          <h2 className="mb-4 font-semibold text-neutral-900">Chỉnh sửa danh mục: {mode.name}</h2>
          <CategoryForm
            initialData={mode}
            onSubmit={(values) => handleUpdate(mode.id, values)}
            onCancel={() => setMode("none")}
            isSubmitting={updateCategory.isPending}
          />
        </div>
      )}

      {/* Thanh công cụ: Tìm kiếm + Chuyển chế độ xem */}
      {categories && categories.length > 0 && (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="relative w-full max-w-xs">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-neutral-400" />
            <Input
              placeholder="Tìm kiếm danh mục..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-9 text-sm bg-white"
            />
          </div>

          <div className="flex items-center rounded-lg border border-neutral-200 bg-white p-1 shadow-2xs">
            <button
              type="button"
              onClick={() => setViewMode("grid")}
              className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition-all ${
                viewMode === "grid"
                  ? "bg-brand-50 text-brand-700 shadow-2xs"
                  : "text-neutral-600 hover:text-neutral-900"
              }`}
            >
              <LayoutGrid className="h-3.5 w-3.5" />
              Dạng thẻ (Rõ ràng)
            </button>
            <button
              type="button"
              onClick={() => setViewMode("table")}
              className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition-all ${
                viewMode === "table"
                  ? "bg-brand-50 text-brand-700 shadow-2xs"
                  : "text-neutral-600 hover:text-neutral-900"
              }`}
            >
              <List className="h-3.5 w-3.5" />
              Dạng bảng
            </button>
          </div>
        </div>
      )}

      {isLoading && <LoadingBlock label="Đang tải danh sách danh mục..." />}
      {isError && <ErrorBlock message="Không tải được danh mục." onRetry={() => refetch()} />}

      {/* HIỂN THỊ DẠNG THẺ (CARD GRID) TRỰC QUAN NHƯ TRANG CÔNG KHAI */}
      {categories && viewMode === "grid" && (
        <div>
          {filteredCategories.length === 0 ? (
            <div className="py-16 text-center text-sm text-neutral-500 rounded-card border border-dashed border-neutral-200 bg-white">
              <FolderTree className="mx-auto mb-2 h-8 w-8 text-neutral-300" />
              Không tìm thấy danh mục nào phù hợp với &quot;{search}&quot;.
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {filteredCategories.map((category) => {
                const theme = pickTheme(category.name);
                return (
                  <Card
                    key={category.id}
                    className="group relative flex flex-col overflow-hidden transition-all duration-300 hover:shadow-lift hover:-translate-y-0.5 border-neutral-200 bg-white"
                  >
                    {/* Phần trên có màu nền gradient & emoji to rõ */}
                    <div className="relative aspect-[16/10] w-full overflow-hidden">
                      {category.imageUrl ? (
                        <Image
                          src={category.imageUrl}
                          alt={category.name}
                          fill
                          sizes="(min-width: 1024px) 25vw, 50vw"
                          className="object-cover transition-transform duration-500 group-hover:scale-105"
                        />
                      ) : (
                        <div
                          className={`flex h-full w-full items-center justify-center bg-gradient-to-br ${theme.bg}`}
                        >
                          <span className="text-5xl transition-transform duration-500 ease-springy group-hover:scale-115">
                            {theme.emoji}
                          </span>
                        </div>
                      )}

                      {/* Badge số công thức góc trên */}
                      <span
                        className={`absolute right-2.5 top-2.5 rounded-full px-2.5 py-0.5 text-xs font-bold shadow-2xs ${theme.badge}`}
                      >
                        {category.recipeCount} công thức
                      </span>
                    </div>

                    {/* Nội dung bên dưới */}
                    <div className="flex flex-1 flex-col justify-between p-4">
                      <div>
                        <h3 className="font-bold text-base text-neutral-900 group-hover:text-brand-700 transition-colors">
                          {category.name}
                        </h3>
                        <p className="mt-1 line-clamp-2 text-xs text-neutral-500 leading-relaxed">
                          {category.description || "Chưa có mô tả cho danh mục này."}
                        </p>
                      </div>

                      {/* Nút hành động */}
                      <div className="mt-4 flex items-center justify-between border-t border-neutral-100 pt-3">
                        <Link
                          href={`/categories/${category.slug}`}
                          target="_blank"
                          className="inline-flex items-center gap-1 text-xs font-semibold text-brand-700 hover:text-brand-800"
                        >
                          Xem món
                          <ExternalLink className="h-3 w-3" />
                        </Link>

                        {isAdmin && (
                          <div className="flex items-center gap-1">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => setMode(category)}
                              className="h-7 w-7 p-0"
                              title="Sửa danh mục"
                            >
                              <Pencil className="h-3.5 w-3.5" />
                            </Button>
                            <Button
                              size="sm"
                              variant="danger"
                              isLoading={deleteCategory.isPending}
                              onClick={() => handleDelete(category)}
                              className="h-7 w-7 p-0"
                              title="Xoá danh mục"
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </Button>
                          </div>
                        )}
                      </div>

                      {deleteErrorId === category.id && (
                        <p className="mt-2 text-xs text-red-600">
                          Không thể xoá — vẫn còn công thức đang dùng.
                        </p>
                      )}
                    </div>
                  </Card>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* HIỂN THỊ DẠNG BẢNG (TABLE) KHI CHỌN CHẾ ĐỘ TABLE */}
      {categories && viewMode === "table" && (
        <div className="overflow-hidden rounded-card border border-neutral-200 bg-white shadow-soft">
          {filteredCategories.length === 0 ? (
            <div className="py-12 text-center text-sm text-neutral-500">
              <FolderTree className="mx-auto mb-2 h-8 w-8 text-neutral-300" />
              Không tìm thấy danh mục nào phù hợp với &quot;{search}&quot;.
            </div>
          ) : (
            <table className="w-full text-left text-sm">
              <thead className="border-b border-neutral-200 bg-neutral-50 text-neutral-600">
                <tr>
                  <th className="px-4 py-3 font-semibold w-12 text-center">#</th>
                  <th className="px-4 py-3 font-semibold">Tên danh mục</th>
                  <th className="px-4 py-3 font-semibold text-center w-28">Số công thức</th>
                  <th className="px-4 py-3 font-semibold">Mô tả</th>
                  <th className="px-4 py-3 font-semibold text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-neutral-100">
                {filteredCategories.map((category, idx) => (
                  <tr key={category.id} className="hover:bg-neutral-50/80 transition-colors">
                    <td className="px-4 py-3.5 text-center text-xs text-neutral-400 font-mono">
                      {idx + 1}
                    </td>
                    <td className="px-4 py-3.5">
                      <div className="flex items-center gap-2">
                        <Link
                          href={`/categories/${category.slug}`}
                          target="_blank"
                          className="font-semibold text-neutral-900 hover:text-brand-600 transition-colors inline-flex items-center gap-1.5"
                        >
                          {category.name}
                          <ExternalLink className="h-3.5 w-3.5 text-neutral-400" />
                        </Link>
                      </div>
                      <p className="text-xs text-neutral-400 mt-0.5 font-mono">/{category.slug}</p>
                    </td>
                    <td className="px-4 py-3.5 text-center">
                      <span
                        className={`inline-flex items-center justify-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                          category.recipeCount > 0
                            ? "bg-herb-50 text-herb-700 border border-herb-200"
                            : "bg-neutral-100 text-neutral-500 border border-neutral-200"
                        }`}
                      >
                        {category.recipeCount} công thức
                      </span>
                    </td>
                    <td className="px-4 py-3.5 text-neutral-600 max-w-xs">
                      <p className="truncate text-xs">{category.description || "—"}</p>
                    </td>
                    <td className="px-4 py-3.5 text-right">
                      {isAdmin ? (
                        <div className="flex justify-end gap-1.5">
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => setMode(category)}
                            title="Sửa danh mục"
                          >
                            <Pencil className="h-3.5 w-3.5" />
                          </Button>
                          <Button
                            size="sm"
                            variant="danger"
                            isLoading={deleteCategory.isPending}
                            onClick={() => handleDelete(category)}
                            title="Xoá danh mục"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                      ) : (
                        <Link
                          href={`/categories/${category.slug}`}
                          target="_blank"
                          className="inline-flex items-center gap-1 text-xs font-medium text-brand-700 hover:underline"
                        >
                          Xem công thức
                          <ExternalLink className="h-3 w-3" />
                        </Link>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}
