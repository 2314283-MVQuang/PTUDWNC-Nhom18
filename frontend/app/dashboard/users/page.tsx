"use client";

import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight, Lock, Search, ShieldAlert, ShieldCheck, Unlock, Users } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { Badge } from "@/components/ui/Badge";
import { ErrorBlock, LoadingBlock } from "@/components/ui/Spinner";
import { useCurrentUser } from "@/hooks/useAuth";
import { useSetUserStatus, useUsers } from "@/hooks/useUsers";
import { apiErrorMessage } from "@/lib/api-client";
import { cn, formatDate } from "@/lib/utils";
import type { UserListParams, UserRole, UserSummary } from "@/types/user";

const ROLE_OPTIONS = [
  { value: "Admin", label: "Quản trị viên" },
  { value: "Author", label: "Tác giả" },
];
const STATUS_OPTIONS = [
  { value: "true", label: "Đang hoạt động" },
  { value: "false", label: "Đã khoá" },
];
const PAGE_SIZE = 20;

/**
 * Tuần 9 (Quang) — "/dashboard/users": Admin xem danh sách người dùng (Admin + tác giả),
 * tìm theo tên/email, lọc vai trò/trạng thái, khoá hoặc mở khoá tài khoản.
 * Tài khoản bị khoá không đăng nhập được (backend kiểm IsActive ở Login/Refresh/Google).
 */
export default function DashboardUsersPage() {
  const { isAdmin, isLoading: sessionLoading, user } = useCurrentUser();
  const [keyword, setKeyword] = useState("");
  const [debounced, setDebounced] = useState("");
  const [role, setRole] = useState<UserRole | "">("");
  const [status, setStatus] = useState<"true" | "false" | "">("");
  const [page, setPage] = useState(1);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(keyword), 350);
    return () => clearTimeout(timer);
  }, [keyword]);

  useEffect(() => setPage(1), [debounced, role, status]);

  const params: UserListParams = { q: debounced, role, isActive: status, page, pageSize: PAGE_SIZE };
  const { data, isLoading, isError, error, refetch, isFetching } = useUsers(params);
  const setUserStatus = useSetUserStatus();

  async function toggle(target: UserSummary) {
    const lock = target.isActive;
    const question = lock
      ? `Khoá tài khoản "${target.displayName}"? Người này sẽ bị đăng xuất và không đăng nhập được cho tới khi mở khoá.`
      : `Mở khoá tài khoản "${target.displayName}"?`;
    if (!confirm(question)) return;
    setActionError(null);
    try {
      await setUserStatus.mutateAsync({ id: target.id, isActive: !lock });
    } catch (err) {
      setActionError(apiErrorMessage(err, "Không cập nhật được trạng thái tài khoản."));
    }
  }

  if (sessionLoading) return <LoadingBlock />;

  if (!isAdmin) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-neutral-200 bg-neutral-50 px-4 py-3 text-sm text-neutral-600">
        <ShieldAlert className="h-4 w-4 text-neutral-500" aria-hidden="true" />
        Chỉ Quản trị viên (Admin) mới xem được danh sách người dùng.
      </div>
    );
  }

  const meta = data?.meta;
  const items = data?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      <div>
        <div className="flex items-center gap-2">
          <h1 className="text-2xl font-bold text-neutral-900">Người dùng</h1>
          {meta && (
            <span className="rounded-full border border-brand-200 bg-brand-50 px-2.5 py-0.5 text-xs font-semibold text-brand-700">
              {meta.total} tài khoản
            </span>
          )}
        </div>
        <p className="mt-1 text-sm text-neutral-500">
          Danh sách quản trị viên và tác giả. Khoá tài khoản để chặn đăng nhập mà vẫn giữ nguyên công thức của họ.
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_180px_180px]">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-neutral-400" aria-hidden="true" />
          <Input
            aria-label="Tìm theo tên hoặc email"
            placeholder="Tìm theo tên, email, tài khoản..."
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            className="bg-white pl-9 text-sm"
          />
        </div>
        <Select
          aria-label="Lọc theo vai trò"
          placeholder="Mọi vai trò"
          options={ROLE_OPTIONS}
          value={role}
          onChange={(e) => setRole(e.target.value as UserRole | "")}
        />
        <Select
          aria-label="Lọc theo trạng thái"
          placeholder="Mọi trạng thái"
          options={STATUS_OPTIONS}
          value={status}
          onChange={(e) => setStatus(e.target.value as "true" | "false" | "")}
        />
      </div>

      {actionError && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-4 py-2.5 text-sm text-red-700">
          {actionError}
        </p>
      )}

      {isLoading && <LoadingBlock label="Đang tải danh sách người dùng..." />}
      {isError && (
        <ErrorBlock message={apiErrorMessage(error, "Không tải được danh sách người dùng.")} onRetry={() => refetch()} />
      )}

      {data && items.length === 0 && (
        <div className="rounded-card border border-dashed border-neutral-200 bg-white py-16 text-center text-sm text-neutral-500">
          <Users className="mx-auto mb-2 h-8 w-8 text-neutral-300" aria-hidden="true" />
          Không có người dùng nào phù hợp.
        </div>
      )}

      {items.length > 0 && (
        <div className={cn("overflow-x-auto rounded-card border border-neutral-200 bg-white", isFetching && "opacity-70")}>
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="border-b border-neutral-200 bg-neutral-50 text-xs uppercase tracking-wide text-neutral-500">
              <tr>
                <th scope="col" className="px-4 py-3 font-semibold">Người dùng</th>
                <th scope="col" className="px-4 py-3 font-semibold">Vai trò</th>
                <th scope="col" className="px-4 py-3 text-right font-semibold">Công thức</th>
                <th scope="col" className="px-4 py-3 font-semibold">Ngày tham gia</th>
                <th scope="col" className="px-4 py-3 font-semibold">Trạng thái</th>
                <th scope="col" className="px-4 py-3 text-right font-semibold">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-neutral-100">
              {items.map((u) => {
                const isSelf = u.email === user?.email;
                const isAdminRow = u.roles.includes("Admin");
                return (
                  <tr key={u.id} className={cn(!u.isActive && "bg-neutral-50 text-neutral-500")}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <span
                          aria-hidden="true"
                          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-100 text-sm font-bold text-brand-700"
                        >
                          {u.displayName.trim().charAt(0).toUpperCase() || "?"}
                        </span>
                        <div className="min-w-0">
                          <p className="truncate font-medium text-neutral-900">
                            {u.displayName}
                            {isSelf && <span className="ml-1.5 text-xs font-normal text-neutral-500">(bạn)</span>}
                          </p>
                          <p className="truncate text-xs text-neutral-500">{u.email}</p>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      {isAdminRow ? (
                        <Badge className="border-purple-200 bg-purple-50 text-purple-700">
                          <ShieldCheck className="h-3 w-3" aria-hidden="true" /> Quản trị viên
                        </Badge>
                      ) : (
                        <Badge className="border-sky-200 bg-sky-50 text-sky-700">Tác giả</Badge>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right tabular-nums">
                      <span className="font-semibold text-neutral-900">{u.recipeCount}</span>
                      <span className="block text-xs text-neutral-500">{u.publishedRecipeCount} đã xuất bản</span>
                    </td>
                    <td className="px-4 py-3 tabular-nums">{formatDate(u.createdAt)}</td>
                    <td className="px-4 py-3">
                      {u.isActive ? (
                        <Badge className="border-emerald-200 bg-emerald-50 text-emerald-700">Hoạt động</Badge>
                      ) : (
                        <Badge className="border-red-200 bg-red-50 text-red-700">Đã khoá</Badge>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      {isSelf ? (
                        <span className="text-xs text-neutral-400">—</span>
                      ) : (
                        <Button
                          size="sm"
                          variant={u.isActive ? "outline" : "secondary"}
                          onClick={() => toggle(u)}
                          disabled={setUserStatus.isPending}
                          aria-label={`${u.isActive ? "Khoá" : "Mở khoá"} tài khoản ${u.displayName}`}
                        >
                          {u.isActive ? (
                            <><Lock className="mr-1.5 h-3.5 w-3.5" aria-hidden="true" />Khoá</>
                          ) : (
                            <><Unlock className="mr-1.5 h-3.5 w-3.5" aria-hidden="true" />Mở khoá</>
                          )}
                        </Button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {meta && meta.totalPages > 1 && (
        <nav className="flex items-center justify-between text-sm text-neutral-600" aria-label="Phân trang người dùng">
          <span>Trang {meta.page}/{meta.totalPages}</span>
          <div className="flex gap-2">
            <Button size="sm" variant="outline" disabled={meta.page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
              <ChevronLeft className="mr-1 h-4 w-4" aria-hidden="true" />Trước
            </Button>
            <Button size="sm" variant="outline" disabled={meta.page >= meta.totalPages} onClick={() => setPage((p) => p + 1)}>
              Sau<ChevronRight className="ml-1 h-4 w-4" aria-hidden="true" />
            </Button>
          </div>
        </nav>
      )}
    </div>
  );
}
