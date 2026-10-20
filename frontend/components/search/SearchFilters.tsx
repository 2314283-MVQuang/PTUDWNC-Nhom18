"use client";

import { useEffect, useId, useState, useTransition, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { SlidersHorizontal } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { SearchFilterProvider, useSearchFilterStore } from "@/stores/search-filter-store";
import { EMPTY_SEARCH_FILTERS, searchHref, validateSearchFilters } from "@/lib/search-filters";
import { DIFFICULTY_LABEL } from "@/lib/utils";
import type { SearchFilterDraft } from "@/types/search";
import type { Category } from "@/types/category";

const inputClass = "mt-1 w-full rounded-xl border border-neutral-300 bg-white px-3 py-2.5 text-sm text-neutral-900 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-200";

function FilterForm({ categories, categoriesUnavailable, onApplied }: {
  categories: Category[]; categoriesUnavailable: boolean; onApplied?: () => void;
}) {
  const { draft, dispatch } = useSearchFilterStore();
  const id = useId();
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [errors, setErrors] = useState<string[]>([]);
  const update = (key: keyof SearchFilterDraft, value: string) => {
    dispatch({ type: "change", key, value });
    setErrors([]);
  };
  const navigate = (value: SearchFilterDraft) => startTransition(() => {
    router.push(searchHref(value));
    onApplied?.();
  });
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const issues = validateSearchFilters(draft);
    if (draft.q.trim().length < 2) issues.unshift("Nhập ít nhất 2 ký tự để tìm công thức.");
    setErrors(issues);
    if (!issues.length) navigate(draft);
  };

  return <form onSubmit={submit} className="space-y-5" aria-label="Lọc công thức" aria-busy={pending}>
    <label className="block text-sm font-medium" htmlFor={`${id}-q`}>Từ khóa
      <input id={`${id}-q`} name="q" type="search" value={draft.q} onChange={e => update("q", e.target.value)} placeholder="Ví dụ: phở bò" className={inputClass} />
    </label>
    <label className="block text-sm font-medium" htmlFor={`${id}-category`}>Danh mục
      <select id={`${id}-category`} name="categoryId" value={draft.categoryId} onChange={e => update("categoryId", e.target.value)} className={inputClass}>
        <option value="">Tất cả danh mục</option>
        {draft.categoryId && !categories.some(c => c.id === draft.categoryId) && <option value={draft.categoryId}>Danh mục trong liên kết</option>}
        {categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}
      </select>
    </label>
    {categoriesUnavailable && <p className="text-sm text-amber-800">Chưa tải được danh mục. Bạn vẫn có thể tìm theo độ khó và thời gian.</p>}
    <label className="block text-sm font-medium" htmlFor={`${id}-difficulty`}>Độ khó
      <select id={`${id}-difficulty`} name="difficulty" value={draft.difficulty} onChange={e => update("difficulty", e.target.value)} className={inputClass}>
        <option value="">Tất cả độ khó</option>
        {draft.difficulty && !(draft.difficulty in DIFFICULTY_LABEL) && <option value={draft.difficulty}>Độ khó không hợp lệ</option>}
        {Object.entries(DIFFICULTY_LABEL).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </select>
    </label>
    {([
      ["Chuẩn bị (phút)", "minPrepTime", "maxPrepTime"],
      ["Nấu (phút)", "minCookTime", "maxCookTime"],
    ] as const).map(([legend, min, max]) => <fieldset key={min}>
      <legend className="text-sm font-medium">{legend}</legend>
      <div className="mt-1 grid grid-cols-2 gap-3">
        {([min, max] as const).map((key, index) => <label key={key} htmlFor={`${id}-${key}`} className="text-xs text-neutral-600">{index === 0 ? "Từ" : "Đến"}
          <input id={`${id}-${key}`} name={key} type="number" min="0" max="2147483647" step="1" value={draft[key]} onChange={e => update(key, e.target.value)} placeholder="Không giới hạn" className={inputClass} />
        </label>)}
      </div>
    </fieldset>)}
    <label className="block text-sm font-medium" htmlFor={`${id}-minServings`}>Khẩu phần tối thiểu
      <input id={`${id}-minServings`} name="minServings" type="number" min="1" max="2147483647" step="1" value={draft.minServings} onChange={e => update("minServings", e.target.value)} placeholder="Không giới hạn" className={inputClass} />
    </label>
    <p className="text-xs leading-relaxed text-neutral-500">Các điều kiện được áp dụng đồng thời. Để trống thời gian nếu không muốn giới hạn; 0 phút nấu phù hợp món không cần nấu.</p>
    {errors.length > 0 && <div role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-800">{errors.map(error => <p key={error}>{error}</p>)}</div>}
    <div className="flex flex-wrap gap-2">
      <Button type="submit" isLoading={pending}>Áp dụng</Button>
      <Button type="button" variant="outline" disabled={pending} onClick={() => {
        dispatch({ type: "reset" });
        setErrors([]);
        navigate({ ...EMPTY_SEARCH_FILTERS, q: draft.q });
      }}>Xóa bộ lọc</Button>
    </div>
  </form>;
}

function FilterPanels({ initial, categories, categoriesUnavailable }: {
  initial: SearchFilterDraft; categories: Category[]; categoriesUnavailable: boolean;
}) {
  const { dispatch } = useSearchFilterStore();
  const [open, setOpen] = useState(false);
  // URL là bộ lọc đã áp dụng; Back/Forward cập nhật cả form desktop và mobile.
  useEffect(() => { dispatch({ type: "sync", value: initial }); }, [initial, dispatch]);
  const formProps = { categories, categoriesUnavailable };
  return <>
    <div className="lg:hidden">
      <Dialog open={open} onOpenChange={setOpen} title="Tìm kiếm và bộ lọc" description="Chọn điều kiện để tìm món phù hợp với thời gian và kỹ năng của bạn."
        trigger={<Button variant="outline"><SlidersHorizontal aria-hidden="true" className="h-4 w-4" />Tìm kiếm và bộ lọc</Button>}>
        <FilterForm {...formProps} onApplied={() => setOpen(false)} />
      </Dialog>
    </div>
    <aside aria-label="Bộ lọc tìm kiếm" className="hidden self-start rounded-card border border-brand-100 bg-white p-5 shadow-soft lg:block">
      <h2 className="mb-5 flex items-center gap-2 font-display text-lg font-semibold"><SlidersHorizontal aria-hidden="true" className="h-5 w-5 text-brand-600" />Tìm món phù hợp</h2>
      <FilterForm {...formProps} />
    </aside>
  </>;
}

export function SearchFilters(props: { initial: SearchFilterDraft; categories: Category[]; categoriesUnavailable: boolean }) {
  return <SearchFilterProvider initial={props.initial}><FilterPanels {...props} /></SearchFilterProvider>;
}
