import Link from "next/link";
import { EmptyState } from "@/components/ui/Feedback";

export default function RecipeNotFound() {
  return <div className="container-page py-12">
    <EmptyState title="Không tìm thấy công thức" message="Công thức không tồn tại hoặc đã được gỡ khỏi trang công khai.">
      <Link href="/recipes" className="text-brand-700 underline">Xem các công thức khác</Link>
    </EmptyState>
  </div>;
}
