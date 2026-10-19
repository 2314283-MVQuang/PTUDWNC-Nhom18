import type { ReactNode } from "react";
import { AlertTriangle, LoaderCircle, SearchX } from "lucide-react";

export function LoadingState({ message = "Đang tải nội dung…" }: { message?: string }) {
  return <div role="status" className="flex items-center justify-center gap-3 rounded-card border border-brand-100 bg-white p-10 text-neutral-600">
    <LoaderCircle aria-hidden="true" className="h-5 w-5 animate-spin" /><span>{message}</span>
  </div>;
}

export function ErrorState({ title = "Chưa tải được nội dung", message, children }: {
  title?: string; message: string; children?: ReactNode;
}) {
  return <div role="alert" className="rounded-card border border-amber-300 bg-amber-50 p-6 text-amber-950">
    <div className="flex items-center gap-2 font-semibold"><AlertTriangle aria-hidden="true" className="h-5 w-5" />{title}</div>
    <p className="mt-2 text-sm">{message}</p>{children && <div className="mt-4">{children}</div>}
  </div>;
}

export function EmptyState({ title, message, children }: { title: string; message: string; children?: ReactNode }) {
  return <div className="rounded-card-lg border border-dashed border-brand-200 bg-white/80 px-6 py-14 text-center">
    <SearchX aria-hidden="true" className="mx-auto h-9 w-9 text-brand-500" />
    <h2 className="mt-4 font-display text-xl font-semibold text-neutral-800">{title}</h2>
    <p className="mx-auto mt-2 max-w-lg text-sm text-neutral-600">{message}</p>
    {children && <div className="mt-4">{children}</div>}
  </div>;
}
