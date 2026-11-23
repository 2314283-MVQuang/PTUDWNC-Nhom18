"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { RecipeEditForm } from "@/components/forms/RecipeEditForm";
import { ErrorBlock, LoadingBlock } from "@/components/ui/Spinner";
import { useCurrentUser } from "@/hooks/useAuth";
import { useRecipe } from "@/hooks/useRecipes";

/**
 * Mục 9: "/dashboard/recipes/[id]/edit" — CSR.
 * Lưu ý cho intern: thư mục đặt tên "[id]" theo đúng route trong tài liệu, NHƯNG giá trị thực
 * tế truyền vào URL là SLUG (RecipeWizardForm/RecipeCard đều link ra dạng
 * `/dashboard/recipes/${recipe.slug}/edit`) — vì backend chỉ có GET /recipes/{slug} (mục 8.3),
 * không có endpoint lấy chi tiết theo id riêng. Dùng useParams() thay vì prop `params` (Promise)
 * vì đây là Client Component.
 *
 * Chỉ tác giả (hoặc Admin — SRS FR-RCP-004/007 "Author-Owner/Admin") mới thấy form. Người khác
 * mở link sửa của bài đã xuất bản sẽ thấy thông báo thay vì form; backend vẫn tự chặn 403.
 */
export default function EditRecipePage() {
  const params = useParams<{ id: string }>();
  const slug = params.id;

  const { data: recipe, isLoading, isError, refetch } = useRecipe(slug);
  const { user, isAdmin } = useCurrentUser();
  const canEdit = !!recipe && (isAdmin || recipe.authorId === user?.id);

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-bold text-neutral-900">Chỉnh sửa công thức</h1>

      {isLoading && <LoadingBlock />}
      {isError && <ErrorBlock message="Không tìm thấy công thức hoặc bạn không có quyền chỉnh sửa." onRetry={() => refetch()} />}
      {recipe && canEdit && <RecipeEditForm recipe={recipe} />}
      {recipe && !canEdit && (
        <div className="rounded-card border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Bạn chỉ chỉnh sửa được công thức do mình đăng.{" "}
          <Link href={`/recipes/${recipe.slug}`} className="font-semibold underline underline-offset-2">
            Xem công thức này
          </Link>
        </div>
      )}
    </div>
  );
}
