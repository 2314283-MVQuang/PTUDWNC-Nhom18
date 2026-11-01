import { revalidatePath } from "next/cache";
import { NextRequest, NextResponse } from "next/server";

/**
 * POST /api/revalidate-recipe
 * Body: { slug: string }
 *
 * Bust ISR cache cho trang công thức cụ thể và danh sách công thức.
 * Được gọi từ RecipeImageManager khi phát hiện dữ liệu ISR cache bị stale
 * (recipe ID trong cache khác với ID thực tế trong DB hiện tại).
 */
export async function POST(request: NextRequest) {
  try {
    const { slug } = (await request.json()) as { slug?: string };
    if (slug) {
      revalidatePath(`/recipes/${slug}`);
    }
    revalidatePath("/recipes");
    return NextResponse.json({ revalidated: true });
  } catch {
    return NextResponse.json({ revalidated: false }, { status: 500 });
  }
}
