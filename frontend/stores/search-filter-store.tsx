"use client";

import { createContext, useContext, useReducer, type Dispatch, type ReactNode } from "react";
import { searchFilterReducer, type SearchFilterAction } from "@/lib/search-filters";
import type { SearchFilterDraft } from "@/types/search";

const SearchFilterContext = createContext<{
  draft: SearchFilterDraft;
  dispatch: Dispatch<SearchFilterAction>;
} | null>(null);

/** Store theo màn hình, không dùng singleton chung cho các request SSR. */
export function SearchFilterProvider({ initial, children }: { initial: SearchFilterDraft; children: ReactNode }) {
  const [draft, dispatch] = useReducer(searchFilterReducer, initial);
  return <SearchFilterContext.Provider value={{ draft, dispatch }}>{children}</SearchFilterContext.Provider>;
}

export function useSearchFilterStore() {
  const store = useContext(SearchFilterContext);
  if (!store) throw new Error("useSearchFilterStore cần SearchFilterProvider");
  return store;
}
