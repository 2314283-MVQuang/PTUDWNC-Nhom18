import type { SearchFilterDraft } from "../types/search";

export const EMPTY_SEARCH_FILTERS: SearchFilterDraft = {
  q: "", categoryId: "", difficulty: "", minPrepTime: "", maxPrepTime: "", minCookTime: "", maxCookTime: "", minServings: "",
};
export const SEARCH_FILTER_KEYS = Object.keys(EMPTY_SEARCH_FILTERS) as (keyof SearchFilterDraft)[];
const TIME_KEYS = ["minPrepTime", "maxPrepTime", "minCookTime", "maxCookTime"] as const;

export function readSearchFilters(params: Record<string, string | string[] | undefined>): SearchFilterDraft {
  const draft = { ...EMPTY_SEARCH_FILTERS };
  for (const key of SEARCH_FILTER_KEYS) {
    const value = params[key];
    draft[key] = (Array.isArray(value) ? value[0] ?? "" : value ?? "").trim();
  }
  return draft;
}

/** Dùng ở server page và form; không âm thầm bỏ qua bộ lọc sai từ URL. */
export function validateSearchFilters(draft: SearchFilterDraft): string[] {
  const errors: string[] = [];
  if (draft.categoryId && (!/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(draft.categoryId)
    || draft.categoryId === "00000000-0000-0000-0000-000000000000"))
    errors.push("Danh mục không hợp lệ. Hãy chọn lại danh mục.");
  if (draft.difficulty && !["Easy", "Medium", "Hard", "Expert"].includes(draft.difficulty))
    errors.push("Hãy chọn một độ khó trong danh sách.");
  for (const key of TIME_KEYS) {
    if (draft[key] && (!/^\d+$/.test(draft[key]) || Number(draft[key]) > 2147483647))
      errors.push("Thời gian phải là số nguyên từ 0 đến 2147483647 phút.");
  }
  if (draft.minServings && (!/^\d+$/.test(draft.minServings) || Number(draft.minServings) < 1
    || Number(draft.minServings) > 2147483647))
    errors.push("Số khẩu phần tối thiểu phải là số nguyên từ 1 trở lên.");
  for (const [min, max, label] of [
    ["minPrepTime", "maxPrepTime", "chuẩn bị"],
    ["minCookTime", "maxCookTime", "nấu"],
  ] as const) {
    if (draft[min] && draft[max] && Number(draft[min]) > Number(draft[max]))
      errors.push(`Thời gian ${label} tối đa phải lớn hơn hoặc bằng tối thiểu.`);
  }
  return [...new Set(errors)];
}

export function searchFilterParams(draft: SearchFilterDraft): Record<string, string> {
  return Object.fromEntries(SEARCH_FILTER_KEYS
    .map(key => [key, draft[key].trim()] as const).filter(([, value]) => value !== ""));
}

export function searchHref(draft: SearchFilterDraft): string {
  const query = new URLSearchParams(searchFilterParams(draft)).toString();
  return query ? `/search?${query}` : "/search";
}

export type SearchFilterAction =
  | { type: "change"; key: keyof SearchFilterDraft; value: string }
  | { type: "sync"; value: SearchFilterDraft }
  | { type: "reset" };

export function searchFilterReducer(state: SearchFilterDraft, action: SearchFilterAction): SearchFilterDraft {
  switch (action.type) {
    case "change": return { ...state, [action.key]: action.value };
    case "sync": return { ...action.value };
    case "reset": return { ...EMPTY_SEARCH_FILTERS, q: state.q };
  }
}
