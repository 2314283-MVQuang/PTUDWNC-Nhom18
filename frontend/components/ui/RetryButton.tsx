"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { Button } from "./Button";

/** refresh tải lại dữ liệu ngay cả khi URL hiện tại không thay đổi. */
export function RetryButton() {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  return <Button variant="outline" isLoading={pending} onClick={() => startTransition(() => router.refresh())}>Thử lại</Button>;
}
