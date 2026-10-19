"use client";

import { ErrorState } from "@/components/ui/Feedback";
import { Button } from "@/components/ui/Button";

export default function SearchError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return <div className="container-page py-16"><ErrorState message="Tìm kiếm đang gặp sự cố. Vui lòng thử lại."><Button onClick={reset}>Thử lại</Button></ErrorState></div>;
}
