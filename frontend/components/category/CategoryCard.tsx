import Image from "next/image";
import Link from "next/link";
import { ArrowUpRight } from "lucide-react";
import { Card } from "@/components/ui/Card";
import { TiltCard } from "@/components/ui/TiltCard";
import type { Category } from "@/types/category";

/**
 * Map từ khoá trong tên danh mục → emoji + màu nền gradient tương ứng.
 */
const CATEGORY_THEME: Array<{
  pattern: RegExp;
  emoji: string;
  bg: string;
  badge: string;
}> = [
  { pattern: /khai vị|salad|gỏi/i,        emoji: "🥗", bg: "from-green-50  to-emerald-100", badge: "bg-emerald-100 text-emerald-700" },
  { pattern: /tráng miệng|ngọt|chè/i,     emoji: "🍮", bg: "from-pink-50   to-rose-100",    badge: "bg-rose-100    text-rose-700" },
  { pattern: /bánh/i,                     emoji: "🍰", bg: "from-amber-50  to-yellow-100",  badge: "bg-yellow-100  text-yellow-700" },
  { pattern: /súp|canh/i,                 emoji: "🍵", bg: "from-sky-50    to-blue-100",    badge: "bg-blue-100    text-blue-700" },
  { pattern: /lẩu|nồi/i,                  emoji: "🍲", bg: "from-orange-50 to-amber-100",   badge: "bg-amber-100   text-amber-700" },
  { pattern: /mì|phở|bún|noodle|miến/i,  emoji: "🍜", bg: "from-yellow-50 to-amber-100",   badge: "bg-amber-100   text-amber-700" },
  { pattern: /nướng|bbq|rang/i,           emoji: "🍖", bg: "from-red-50    to-orange-100",  badge: "bg-orange-100  text-orange-700" },
  { pattern: /hải sản|cá|tôm|mực/i,      emoji: "🦐", bg: "from-cyan-50   to-sky-100",     badge: "bg-sky-100     text-sky-700" },
  { pattern: /chay/i,                     emoji: "🥬", bg: "from-lime-50   to-green-100",   badge: "bg-green-100   text-green-700" },
  { pattern: /đồ uống|nước|sinh tố/i,    emoji: "🥤", bg: "from-violet-50 to-purple-100",  badge: "bg-purple-100  text-purple-700" },
  { pattern: /sáng|breakfast/i,           emoji: "🍳", bg: "from-yellow-50 to-orange-100",  badge: "bg-orange-100  text-orange-700" },
  { pattern: /chiên|xào/i,               emoji: "🥘", bg: "from-orange-50 to-red-100",     badge: "bg-red-100     text-red-700" },
  { pattern: /hấp/i,                      emoji: "♨️", bg: "from-teal-50   to-cyan-100",    badge: "bg-teal-100    text-teal-700" },
  { pattern: /kho/i,                      emoji: "🍛", bg: "from-amber-50  to-orange-100",  badge: "bg-orange-100  text-orange-700" },
  { pattern: /Nhật|Hàn|Tàu|Ý|Tây|nước ngoài/i, emoji: "🌍", bg: "from-indigo-50 to-blue-100", badge: "bg-indigo-100 text-indigo-700" },
  { pattern: /eat clean|healthy/i,        emoji: "🥑", bg: "from-green-50  to-lime-100",   badge: "bg-lime-100    text-lime-700" },
];

function pickTheme(name: string) {
  for (const theme of CATEGORY_THEME) {
    if (theme.pattern.test(name)) return theme;
  }
  return { emoji: "🍽️", bg: "from-brand-50 to-orange-100", badge: "bg-orange-100 text-orange-700" };
}

/** Card danh mục — dùng ở trang chủ và /categories. */
export function CategoryCard({ category }: { category: Category }) {
  const theme = pickTheme(category.name);

  return (
    <TiltCard maxTilt={9} className="h-full">
      <Link href={`/categories/${category.slug}`} className="block h-full">
        <Card className="group relative h-full overflow-hidden">
          {/* Vùng ảnh / emoji */}
          <div className="relative aspect-[16/10] w-full overflow-hidden bg-brand-50">
            {category.imageUrl ? (
              <Image
                src={category.imageUrl}
                alt={category.name}
                fill
                sizes="(min-width: 1024px) 25vw, 50vw"
                className="object-cover transition-transform duration-700 ease-springy group-hover:scale-110"
              />
            ) : (
              <div className={`flex h-full w-full items-center justify-center bg-gradient-to-br ${theme.bg}`}>
                <span className="text-5xl transition-transform duration-500 ease-springy group-hover:-rotate-12 group-hover:scale-125">
                  {theme.emoji}
                </span>
              </div>
            )}

            {/* Badge số công thức nổi góc trên phải */}
            <span
              className={`absolute right-2 top-2 rounded-full px-2.5 py-0.5 text-xs font-bold shadow-sm ${theme.badge}`}
            >
              {category.recipeCount} CT
            </span>

            {/* Lớp phủ cam đậm dần khi hover */}
            <div className="pointer-events-none absolute inset-0 bg-brand-gradient opacity-0 mix-blend-multiply transition-opacity duration-500 group-hover:opacity-20" />
          </div>

          {/* Footer card */}
          <div className="flex items-center justify-between gap-2 p-4">
            <span className="min-w-0">
              <h3 className="truncate font-display text-base font-bold text-neutral-900 transition-colors duration-300 group-hover:text-brand-700">
                {category.name}
              </h3>
              {category.description ? (
                <p className="mt-0.5 line-clamp-1 text-xs text-neutral-500">{category.description}</p>
              ) : (
                <p className="mt-0.5 text-xs text-neutral-400">{category.recipeCount} công thức</p>
              )}
            </span>

            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-brand-50 text-brand-600 transition-all duration-300 ease-springy group-hover:bg-brand-gradient group-hover:text-white">
              <ArrowUpRight className="h-4 w-4" />
            </span>
          </div>
        </Card>
      </Link>
    </TiltCard>
  );
}
