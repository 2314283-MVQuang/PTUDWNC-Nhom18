import Link from "next/link";
import { Clock, CookingPot, Users } from "lucide-react";
import { Card } from "@/components/ui/Card";
import { DifficultyBadge } from "@/components/recipe/DifficultyBadge";
import { formatMinutes } from "@/lib/utils";
import type { SearchRecipe } from "@/types/search";

export function SearchResults({ recipes }: { recipes: SearchRecipe[] }) {
  return <ul className="grid gap-5 sm:grid-cols-2" aria-label="Công thức tìm thấy">
    {recipes.map(recipe => <li key={recipe.id}>
      <Card className="flex h-full flex-col p-5">
        <div className="flex items-center justify-between gap-3"><CookingPot aria-hidden="true" className="h-8 w-8 text-brand-500" /><DifficultyBadge difficulty={recipe.difficulty} /></div>
        <h2 className="mt-4 font-display text-xl font-semibold text-neutral-900"><Link className="rounded hover:text-brand-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-brand-500" href={`/recipes/${encodeURIComponent(recipe.slug)}`}>{recipe.title}</Link></h2>
        <p className="mt-2 line-clamp-3 text-sm leading-relaxed text-neutral-600">{recipe.description}</p>
        <div className="mt-auto flex flex-wrap gap-x-4 gap-y-2 pt-5 text-xs text-neutral-600">
          <span className="flex items-center gap-1"><Clock aria-hidden="true" className="h-4 w-4" />Chuẩn bị: {formatMinutes(recipe.prepTime)}</span>
          <span>Nấu: {formatMinutes(recipe.cookTime)}</span>
          <span className="flex items-center gap-1"><Users aria-hidden="true" className="h-4 w-4" />{recipe.servings} phần</span>
        </div>
      </Card>
    </li>)}
  </ul>;
}
