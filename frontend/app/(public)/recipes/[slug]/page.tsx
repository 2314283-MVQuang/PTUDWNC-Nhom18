import Image from "next/image";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ChefHat, ChevronRight, Clock, Flame, ListOrdered, Timer, Users } from "lucide-react";
import { apiFetch, ApiError } from "@/lib/api-client";
import { DifficultyBadge } from "@/components/recipe/DifficultyBadge";
import { IngredientList } from "@/components/recipe/IngredientList";
import { StepList } from "@/components/recipe/StepList";
import { NutritionTable } from "@/components/recipe/NutritionTable";
import { formatMinutes, resolveMediaUrl } from "@/lib/utils";
import type { RecipeDetail } from "@/types/recipe";
import type { Category } from "@/types/category";
import type { Metadata } from "next";
import { serializeJsonLd } from "@/lib/structured-data";

// Mục 9: "/recipes/[slug]" — ISR revalidate=300 (5 phút).
export const revalidate = 300;

interface RecipePageProps {
  params: Promise<{ slug: string }>;
}

/**
 * Bản chi tiết KHÔNG có sẵn primaryImageUrl như bản danh sách — phải tự chọn từ mảng images:
 * ưu tiên ảnh được đánh dấu đại diện, không có thì lấy ảnh đầu tiên.
 */
/** Tra slug danh mục từ id. Lỗi mạng thì trả null và trang hiển thị tên danh mục không kèm link. */
async function getCategorySlug(categoryId: string): Promise<string | null> {
  try {
    const categories = await apiFetch<Category[]>("/categories", { next: { revalidate } });
    return categories.find((category) => category.id === categoryId)?.slug ?? null;
  } catch {
    return null;
  }
}

function getCoverUrl(recipe: RecipeDetail): string | null {
  const primary = recipe.images.find((image) => image.isPrimary) ?? recipe.images[0];
  return resolveMediaUrl(primary?.originalUrl);
}

async function getRecipe(slug: string): Promise<RecipeDetail | null> {
  try {
    return await apiFetch<RecipeDetail>(`/recipes/${slug}`, { next: { revalidate } });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}

export async function generateMetadata({ params }: RecipePageProps): Promise<Metadata> {
  const { slug } = await params;
  const recipe = await getRecipe(slug);
  if (!recipe) return { title: "Không tìm thấy công thức" };

  const cover = getCoverUrl(recipe);

  // Mục 5.7 SEO: title <=60 ký tự, meta description 150-160 ký tự, Open Graph đầy đủ.
  return {
    title: recipe.title,
    description: recipe.description.slice(0, 160),
    openGraph: {
      title: recipe.title,
      description: recipe.description.slice(0, 160),
      images: cover ? [{ url: cover, width: 1200, height: 630 }] : undefined,
      type: "article",
    },
  };
}

export default async function RecipeDetailPage({ params }: RecipePageProps) {
  const { slug } = await params;
  const recipe = await getRecipe(slug);
  if (!recipe) notFound();

  const totalTime = recipe.prepTime + recipe.cookTime;
  const cover = getCoverUrl(recipe);

  // Bản chi tiết chỉ có categoryId + categoryName, không có slug danh mục, mà route danh mục lại
  // là "/categories/[slug]". Nạp danh sách danh mục (nhỏ, đã cache ISR) rồi tra ngược ra slug.
  const categorySlug = await getCategorySlug(recipe.categoryId);

  // JSON-LD Schema.org Recipe markup (mục 5.7) — giúp Google hiển thị Rich Results
  // (ảnh, thời gian nấu, đánh giá...) ngay trên trang kết quả tìm kiếm.
  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "Recipe",
    name: recipe.title,
    description: recipe.description,
    image: cover ? [cover] : undefined,
    author: { "@type": "Person", name: recipe.authorDisplayName },
    datePublished: recipe.publishedAt ?? undefined,
    prepTime: `PT${recipe.prepTime}M`,
    cookTime: `PT${recipe.cookTime}M`,
    totalTime: `PT${totalTime}M`,
    recipeYield: `${recipe.servings} khẩu phần`,
    recipeIngredient: recipe.ingredients.map((i) => `${i.quantity ?? ""} ${i.unit ?? ""} ${i.name}`.trim()),
    recipeInstructions: recipe.steps.map((s) => ({
      "@type": "HowToStep",
      name: s.title,
      text: s.description,
    })),
    ...(recipe.nutrition.calories != null && {
      nutrition: { "@type": "NutritionInformation", calories: `${recipe.nutrition.calories} kcal` },
    }),
  };

  /** Các chỉ số hiện thành dải thẻ nhỏ ngay dưới ảnh bìa. */
  const metaTiles = [
    { icon: Timer, label: "Sơ chế", value: formatMinutes(recipe.prepTime) },
    { icon: Flame, label: "Nấu", value: formatMinutes(recipe.cookTime) },
    { icon: Clock, label: "Tổng cộng", value: formatMinutes(totalTime) },
    { icon: Users, label: "Khẩu phần", value: `${recipe.servings} phần` },
  ];

  return (
    <article className="flex flex-col pb-20">
      {/* JSON-LD bắt buộc phải bơm bằng dangerouslySetInnerHTML — đây là cách Next.js khuyến nghị. */}
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: serializeJsonLd(jsonLd) }} />

      {/* ---------- Ảnh bìa + tiêu đề đè lên ảnh ---------- */}
      <header className="relative isolate overflow-hidden bg-brand-gradient">
        {cover && (
          <Image
            src={cover}
            alt={recipe.title}
            fill
            priority
            unoptimized
            sizes="100vw"
            className="object-cover"
          />
        )}
        {/* Lớp phủ tối để chữ trắng luôn đọc được dù ảnh sáng hay tối */}
        <div className="absolute inset-0 bg-gradient-to-t from-neutral-900/90 via-neutral-900/55 to-neutral-900/25" />

        <div className="container-page relative pb-10 pt-16 sm:pb-14 sm:pt-24">
          {/* Đường dẫn phân cấp */}
          <nav className="mb-4 flex animate-fade-up items-center gap-1.5 text-xs text-white/70">
            <Link href="/" className="transition-colors hover:text-white">
              Trang chủ
            </Link>
            <ChevronRight className="h-3.5 w-3.5" />
            <Link href="/categories" className="transition-colors hover:text-white">
              Danh mục
            </Link>
            <ChevronRight className="h-3.5 w-3.5" />
            {categorySlug ? (
              <Link
                href={`/categories/${categorySlug}`}
                className="truncate transition-colors hover:text-white"
              >
                {recipe.categoryName}
              </Link>
            ) : (
              <span className="truncate">{recipe.categoryName}</span>
            )}
          </nav>

          <div className="flex animate-fade-up flex-wrap items-center gap-3" style={{ animationDelay: "60ms" }}>
            <DifficultyBadge difficulty={recipe.difficulty} />
            <span className="rounded-full border border-white/30 bg-white/15 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-white backdrop-blur-md">
              {recipe.categoryName}
            </span>
          </div>

          <h1
            className="mt-4 max-w-3xl animate-fade-up text-3xl font-bold leading-tight text-white sm:text-5xl"
            style={{ animationDelay: "120ms" }}
          >
            {recipe.title}
          </h1>
          <p
            className="mt-4 max-w-2xl animate-fade-up leading-relaxed text-white/85"
            style={{ animationDelay: "180ms" }}
          >
            {recipe.description}
          </p>

          <div
            className="mt-6 flex animate-fade-up items-center gap-2.5 text-sm text-white/80"
            style={{ animationDelay: "240ms" }}
          >
            <span className="flex h-9 w-9 items-center justify-center rounded-full bg-white/20 text-white backdrop-blur-md">
              <ChefHat className="h-4 w-4" />
            </span>
            <span>
              Công thức bởi <strong className="font-semibold text-white">{recipe.authorDisplayName}</strong>
            </span>
          </div>
        </div>
      </header>

      {/* ---------- Dải thẻ chỉ số, nhô lên đè mép ảnh bìa ---------- */}
      <div className="container-page -mt-8 sm:-mt-10">
        <div className="grid grid-cols-2 gap-px overflow-hidden rounded-card-lg bg-brand-100 shadow-lift lg:grid-cols-4">
          {metaTiles.map(({ icon: Icon, label, value }) => (
            <div
              key={label}
              className="group flex items-center gap-3 bg-white px-5 py-5 transition-colors duration-300 hover:bg-brand-50"
            >
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-50 text-brand-600 transition-all duration-300 ease-springy group-hover:scale-110 group-hover:bg-brand-gradient group-hover:text-white">
                <Icon className="h-5 w-5" />
              </span>
              <span className="min-w-0">
                <span className="block text-xs text-neutral-500">{label}</span>
                <span className="block truncate font-semibold text-neutral-900">{value}</span>
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* ---------- Nội dung chính ---------- */}
      <div className="container-page mt-12 grid grid-cols-1 gap-10 lg:grid-cols-3">
        <div className="flex flex-col gap-8 lg:col-span-2">
          {recipe.instructions && <section aria-label="Hướng dẫn bổ sung">
            <h2 className="mb-4 text-xl font-bold">Hướng dẫn bổ sung</h2>
            <p className="whitespace-pre-line leading-relaxed text-neutral-700">{recipe.instructions}</p>
          </section>}
          <section>
            <h2 className="mb-5 flex items-center gap-3 text-xl font-bold sm:text-2xl">
              <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-gradient text-white shadow-soft">
                <ListOrdered className="h-5 w-5" />
              </span>
              Các bước thực hiện
            </h2>
            <StepList steps={recipe.steps} />
          </section>
        </div>

        {/* Cột phải dính theo màn hình khi cuộn — đọc bước nấu vẫn thấy nguyên liệu */}
        <aside className="flex flex-col gap-6 lg:sticky lg:top-28 lg:self-start">
          <section className="rounded-card-lg border border-brand-100 bg-white p-5 shadow-soft">
            <h2 className="mb-4 flex items-center gap-2 text-lg font-bold">
              <span className="text-xl" aria-hidden>
                🧺
              </span>
              Nguyên liệu
            </h2>
            <IngredientList ingredients={recipe.ingredients} />
          </section>

          <section className="rounded-card-lg border border-brand-100 bg-white p-5 shadow-soft">
            <h2 className="mb-4 flex items-center gap-2 text-lg font-bold">
              <span className="text-xl" aria-hidden>
                📊
              </span>
              Dinh dưỡng / khẩu phần
            </h2>
            <NutritionTable nutrition={recipe.nutrition} />
          </section>

          <Link
            href="/recipes"
            className="group inline-flex items-center justify-center gap-2 rounded-xl border border-brand-200 bg-white px-4 py-3 text-sm font-medium text-brand-700 shadow-soft transition-all duration-300 ease-springy hover:-translate-y-0.5 hover:shadow-lift"
          >
            <ChevronRight className="h-4 w-4 rotate-180 transition-transform duration-300 group-hover:-translate-x-1" />
            Xem công thức khác
          </Link>
        </aside>
      </div>
    </article>
  );
}
