"use client";

import { ErrorState } from "@/components/ui/Feedback";
import { Button } from "@/components/ui/Button";
import { useRouter } from "next/navigation";
import { useTransition } from "react";

export default function PublicError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  return <div className="container-page py-12">
    <ErrorState message="Nội dung đang tạm thời gián đoạn. Vui lòng thử lại.">
      <Button isLoading={pending} onClick={() => startTransition(() => { router.refresh(); reset(); })}>Thử lại</Button>
    </ErrorState>
  </div>;
}
