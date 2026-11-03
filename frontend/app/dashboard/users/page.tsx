"use client";

import { Suspense, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Users, Search } from "lucide-react";
import { useCurrentUser } from "@/hooks/useAuth";
import { apiFetchPaged, apiErrorMessage } from "@/lib/api-client";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { ErrorBlock, LoadingBlock } from "@/components/ui/Spinner";
import { Pagination } from "@/components/ui/Pagination";
import { formatDate } from "@/lib/utils";
import type { AdminUser } from "@/types/admin";

export default function UsersPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <UserDirectory />
    </Suspense>
  );
}

function UserDirectory() {
  const router = useRouter();
  const params = useSearchParams();
  const {
    user,
    isAdmin,
    accessToken,
    isLoading: sessionLoading,
  } = useCurrentUser();
  const q = params.get("q") ?? "";
  const role = params.get("role") ?? "";
  const page = Math.max(1, Number(params.get("page")) || 1);
  const [search, setSearch] = useState(q);
  const href = (changes: Record<string, string>) => {
    const query = new URLSearchParams(params.toString());
    for (const [key, value] of Object.entries(changes)) {
      if (value) query.set(key, value);
      else query.delete(key);
    }
    return `/dashboard/users?${query}`;
  };
  const result = useQuery({
    queryKey: ["admin-users", user?.id, page, role, q],
    queryFn: () =>
      apiFetchPaged<AdminUser>("/users", {
        token: accessToken,
        params: {
          page,
          pageSize: 12,
          role: role || undefined,
          q: q || undefined,
        },
        cache: "no-store",
      }),
    enabled: !sessionLoading && isAdmin && !!accessToken,
  });
  if (sessionLoading) return <LoadingBlock />;
  if (!isAdmin)
    return (
      <ErrorBlock message="Chỉ quản trị viên được xem danh sách người dùng." />
    );
  return (
    <section className="space-y-6">
      <div className="rounded-card-lg border border-brand-100 bg-gradient-to-r from-brand-50 to-white p-6">
        <div className="mb-3 inline-flex rounded-xl bg-brand-100 p-3 text-brand-700">
          <Users className="h-6 w-6" />
        </div>
        <h1 className="text-2xl font-bold">Người dùng</h1>
        <p className="mt-2 text-sm text-neutral-600">
          Tra cứu tài khoản và vai trò trong cộng đồng Culinary Blog.
        </p>
      </div>
      <form
        className="flex flex-col gap-3 rounded-card border border-neutral-200 bg-white p-4 sm:flex-row sm:items-end"
        onSubmit={(event) => {
          event.preventDefault();
          router.push(href({ q: search.trim(), page: "1" }));
        }}
      >
        <div className="flex-1">
          <Input
            name="q"
            label="Tìm người dùng"
            placeholder="Tên hiển thị hoặc email"
            value={search}
            maxLength={100}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>
        <Select
          name="role"
          label="Vai trò"
          value={role}
          options={[
            { value: "", label: "Tất cả vai trò" },
            { value: "Admin", label: "Quản trị viên" },
            { value: "Author", label: "Tác giả" },
          ]}
          onChange={(event) =>
            router.push(href({ role: event.target.value, page: "1" }))
          }
        />
        <Button type="submit">
          <Search className="h-4 w-4" />
          Tìm kiếm
        </Button>
      </form>
      {result.isLoading && <LoadingBlock />}
      {result.isError && (
        <ErrorBlock
          message={apiErrorMessage(result.error, "Không tải được người dùng.")}
          onRetry={() => result.refetch()}
        />
      )}
      {result.data && (
        <>
          <p className="text-sm text-neutral-500">
            {result.data.meta.total} tài khoản · Trang {result.data.meta.page} /{" "}
            {Math.max(1, result.data.meta.totalPages)}
          </p>
          {result.data.items.length === 0 ? (
            <div className="rounded-card border border-dashed p-10 text-center text-neutral-500">
              Không có người dùng phù hợp.
            </div>
          ) : (
            <div
              role="region"
              aria-label="Danh sách người dùng, cuộn ngang để xem đủ cột"
              tabIndex={0}
              className="overflow-x-auto rounded-card border border-neutral-200 bg-white"
            >
              <table className="w-full min-w-[640px] text-left text-sm">
                <thead className="bg-neutral-50 text-neutral-600">
                  <tr>
                    {[
                      "Người dùng",
                      "Vai trò",
                      "Trạng thái",
                      "Ngày tham gia",
                    ].map((label) => (
                      <th key={label} className="px-4 py-3 font-medium">
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-neutral-100">
                  {result.data.items.map((account) => (
                    <tr key={account.id}>
                      <td className="px-4 py-4">
                        <p className="font-semibold text-neutral-900">
                          {account.displayName}
                        </p>
                        <p className="mt-1 break-all text-neutral-500">
                          {account.email}
                        </p>
                      </td>
                      <td className="px-4 py-4">
                        <div className="flex flex-wrap gap-1">
                          {account.roles.map((value) => (
                            <span
                              key={value}
                              className="rounded-full bg-brand-50 px-2 py-1 text-xs font-medium text-brand-800"
                            >
                              {value === "Admin"
                                ? "Quản trị viên"
                                : value === "Author"
                                  ? "Tác giả"
                                  : value}
                            </span>
                          ))}
                        </div>
                      </td>
                      <td className="px-4 py-4">
                        <span
                          className={
                            account.isActive
                              ? "text-herb-700"
                              : "text-neutral-500"
                          }
                        >
                          {account.isActive ? "Hoạt động" : "Ngừng hoạt động"}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-4 py-4 text-neutral-500">
                        {formatDate(account.createdAt)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <Pagination
            meta={result.data.meta}
            buildHref={(number) => href({ page: String(number) })}
          />
        </>
      )}
    </section>
  );
}
