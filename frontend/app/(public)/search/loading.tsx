import { LoadingState } from "@/components/ui/Feedback";

export default function SearchLoading() {
  return <div className="container-page py-16"><LoadingState message="Đang tìm công thức phù hợp…" /></div>;
}
