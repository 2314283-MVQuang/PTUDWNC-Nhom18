"use client";

import { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { MessageCircle, Pencil, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { ErrorBlock, LoadingBlock } from "@/components/ui/Spinner";
import { useCurrentUser } from "@/hooks/useAuth";
import { useAddComment, useDeleteComment, useRecipeComments, useUpdateComment } from "@/hooks/useComments";
import { apiErrorMessage } from "@/lib/api-client";
import { cn } from "@/lib/utils";
import { COMMENT_MAX_LENGTH, validateCommentContent } from "@/lib/comments";
import type { RecipeComment } from "@/types/comment";

function formatWhen(iso: string): string {
  return new Date(iso).toLocaleString("vi-VN", {
    day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit",
  });
}

/**
 * Tuần 9 (Quang) — FR-CMT: khu bình luận dưới trang công thức.
 * Khách xem được; muốn viết phải đăng nhập. Nội dung hiển thị dạng text (React tự escape, không
 * dùng dangerouslySetInnerHTML) nên không bị chèn script (XSS).
 */
export function RecipeComments({ recipeId }: { recipeId: string }) {
  const pathname = usePathname();
  const { isAuthenticated, isLoading: sessionLoading } = useCurrentUser();
  const [page, setPage] = useState(1);
  const [draft, setDraft] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const { data, isLoading, isError, error, refetch } = useRecipeComments(recipeId, page);
  const addComment = useAddComment(recipeId);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const invalid = validateCommentContent(draft);
    if (invalid) return setFormError(invalid);
    setFormError(null);
    try {
      await addComment.mutateAsync(draft.trim());
      setDraft("");
      setPage(1);
    } catch (err) {
      setFormError(apiErrorMessage(err, "Không gửi được bình luận. Vui lòng thử lại."));
    }
  }

  const total = data?.meta.total ?? 0;

  return (
    <section aria-labelledby="comments-heading" className="flex flex-col gap-5">
      <h2 id="comments-heading" className="flex items-center gap-3 text-xl font-bold sm:text-2xl">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-gradient text-white shadow-soft">
          <MessageCircle className="h-5 w-5" aria-hidden="true" />
        </span>
        Bình luận {data && <span className="text-base font-medium text-neutral-500">({total})</span>}
      </h2>

      {sessionLoading ? null : isAuthenticated ? (
        <form onSubmit={submit} className="flex flex-col gap-2 rounded-card border border-brand-100 bg-white p-4 shadow-soft">
          <Textarea
            id="new-comment"
            aria-label="Viết bình luận"
            placeholder="Chia sẻ cảm nhận, mẹo nấu hoặc câu hỏi về công thức này..."
            rows={3}
            value={draft}
            maxLength={COMMENT_MAX_LENGTH}
            onChange={(e) => setDraft(e.target.value)}
            error={formError ?? undefined}
          />
          <div className="flex items-center justify-between gap-3">
            <span className="text-xs text-neutral-400 tabular-nums">{draft.trim().length}/{COMMENT_MAX_LENGTH}</span>
            <Button type="submit" size="sm" isLoading={addComment.isPending} disabled={!draft.trim()}>
              Gửi bình luận
            </Button>
          </div>
        </form>
      ) : (
        <p className="rounded-card border border-dashed border-brand-200 bg-brand-50/40 px-4 py-3 text-sm text-neutral-600">
          <Link href={`/login?callbackUrl=${encodeURIComponent(pathname)}`} className="font-semibold text-brand-700 underline underline-offset-2">
            Đăng nhập
          </Link>{" "}
          để bình luận về công thức này.
        </p>
      )}

      {isLoading && <LoadingBlock label="Đang tải bình luận..." />}
      {isError && <ErrorBlock message={apiErrorMessage(error, "Không tải được bình luận.")} onRetry={() => refetch()} />}
      {data && data.items.length === 0 && (
        <p className="text-sm text-neutral-500">Chưa có bình luận nào. Hãy là người đầu tiên!</p>
      )}

      {data && data.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {data.items.map((comment) => (
            <CommentItem key={comment.id} comment={comment} recipeId={recipeId} />
          ))}
        </ul>
      )}

      {data && data.meta.totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-neutral-600">
          <span>Trang {data.meta.page}/{data.meta.totalPages}</span>
          <div className="flex gap-2">
            <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Mới hơn</Button>
            <Button size="sm" variant="outline" disabled={page >= data.meta.totalPages} onClick={() => setPage((p) => p + 1)}>Cũ hơn</Button>
          </div>
        </div>
      )}
    </section>
  );
}

function CommentItem({ comment, recipeId }: { comment: RecipeComment; recipeId: string }) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(comment.content);
  const [error, setError] = useState<string | null>(null);
  const updateComment = useUpdateComment(recipeId);
  const deleteComment = useDeleteComment(recipeId);

  async function save() {
    const invalid = validateCommentContent(value);
    if (invalid) return setError(invalid);
    try {
      await updateComment.mutateAsync({ id: comment.id, content: value.trim() });
      setEditing(false);
      setError(null);
    } catch (err) {
      setError(apiErrorMessage(err, "Không lưu được bình luận."));
    }
  }

  async function remove() {
    if (!confirm("Xoá bình luận này?")) return;
    try {
      await deleteComment.mutateAsync(comment.id);
    } catch (err) {
      setError(apiErrorMessage(err, "Không xoá được bình luận."));
    }
  }

  return (
    <li className="flex gap-3 rounded-card border border-neutral-200 bg-white p-4">
      <span
        aria-hidden="true"
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-100 text-sm font-bold text-brand-700"
      >
        {comment.author.displayName.trim().charAt(0).toUpperCase() || "?"}
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-baseline gap-x-2">
          <span className="font-semibold text-neutral-900">{comment.author.displayName}</span>
          <time dateTime={comment.createdAt} className="text-xs text-neutral-500">{formatWhen(comment.createdAt)}</time>
          {comment.isEdited && <span className="text-xs text-neutral-400">(đã chỉnh sửa)</span>}
        </div>

        {editing ? (
          <div className="mt-2 flex flex-col gap-2">
            <Textarea
              aria-label="Sửa bình luận"
              rows={3}
              value={value}
              maxLength={COMMENT_MAX_LENGTH}
              onChange={(e) => setValue(e.target.value)}
              error={error ?? undefined}
            />
            <div className="flex gap-2">
              <Button size="sm" onClick={save} isLoading={updateComment.isPending}>Lưu</Button>
              <Button size="sm" variant="ghost" onClick={() => { setEditing(false); setValue(comment.content); setError(null); }}>
                Huỷ
              </Button>
            </div>
          </div>
        ) : (
          <p className="mt-1 whitespace-pre-line break-words text-sm leading-relaxed text-neutral-700">{comment.content}</p>
        )}

        {!editing && error && <p role="alert" className="mt-1 text-xs font-medium text-spice-600">{error}</p>}

        {!editing && (comment.canEdit || comment.canDelete) && (
          <div className="mt-2 flex gap-3 text-xs">
            {comment.canEdit && (
              <button type="button" onClick={() => setEditing(true)} className="inline-flex items-center gap-1 font-medium text-neutral-500 hover:text-brand-700">
                <Pencil className="h-3.5 w-3.5" aria-hidden="true" /> Sửa
              </button>
            )}
            {comment.canDelete && (
              <button
                type="button"
                onClick={remove}
                disabled={deleteComment.isPending}
                className={cn("inline-flex items-center gap-1 font-medium text-neutral-500 hover:text-spice-600", deleteComment.isPending && "opacity-50")}
              >
                <Trash2 className="h-3.5 w-3.5" aria-hidden="true" /> Xoá
              </button>
            )}
          </div>
        )}
      </div>
    </li>
  );
}
