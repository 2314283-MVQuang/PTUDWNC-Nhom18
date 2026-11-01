"use client";

import { useRef, useState, useEffect } from "react";
import { createPortal } from "react-dom";
import { useRouter } from "next/navigation";
import { Camera, ImagePlus, Loader2, Sparkles, X } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { ImagesEditor } from "@/components/forms/ImagesEditor";
import { useCurrentUser } from "@/hooks/useAuth";
import { uploadRecipeImage, validateImageFile } from "@/lib/recipe-images";
import { apiErrorMessage } from "@/lib/api-client";
import type { RecipeDetail } from "@/types/recipe";

export function RecipeImageManager({ recipe }: { recipe: RecipeDetail }) {
  const { accessToken } = useCurrentUser();
  const router = useRouter();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [isOpen, setIsOpen] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  const hasImages = recipe.images && recipe.images.length > 0;

  async function handleDirectFileSelect(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? []);
    if (files.length === 0) return;

    setError(null);
    try {
      setIsUploading(true);
      for (const file of files) {
        const invalid = validateImageFile(file);
        if (invalid) {
          setError(invalid);
          continue;
        }
        await uploadRecipeImage(recipe.id, file, { token: accessToken });
      }
      router.refresh();
    } catch (err) {
      setError(apiErrorMessage(err, "Không tải được ảnh lên từ máy tính."));
    } finally {
      setIsUploading(false);
      e.target.value = "";
    }
  }

  function handleClose() {
    setIsOpen(false);
    router.refresh();
  }

  // Modal dùng createPortal để thoát khỏi stacking context của <header isolate>
  // Tránh bị bảng stats (z-20) đè lên do isolate context của header
  const modal = isOpen && mounted
    ? createPortal(
        <div
          className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
          onClick={handleClose}
          style={{ animation: "fadeIn 0.15s ease" }}
        >
          <div
            className="relative w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-neutral-200 pb-3 mb-4">
              <div>
                <h3 className="text-lg font-bold text-neutral-900">Chỉnh sửa ảnh công thức</h3>
                <p className="text-xs text-neutral-500">
                  {recipe.title} — Thêm ảnh từ máy, đặt ảnh đại diện hoặc xóa ảnh cũ.
                </p>
              </div>

              <button
                type="button"
                onClick={handleClose}
                className="rounded-lg p-1.5 text-neutral-400 hover:bg-neutral-100 hover:text-neutral-700 transition-colors"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <ImagesEditor recipeId={recipe.id} images={recipe.images} />

            <div className="mt-6 flex justify-end border-t border-neutral-200 pt-3">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleClose}
              >
                Xong
              </Button>
            </div>
          </div>
        </div>,
        document.body
      )
    : null;

  return (
    <>
      <input
        ref={fileInputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp,image/gif"
        multiple
        className="hidden"
        onChange={handleDirectFileSelect}
      />

      {/* 1. Nếu công thức CHƯA có ảnh: hiển thị khung tải ảnh nổi bật ngay tại bìa */}
      {!hasImages && (
        <div className="mb-6 rounded-2xl border border-white/20 bg-black/40 p-5 text-white backdrop-blur-md">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-brand-500/80 text-white shadow-md">
                <Camera className="h-6 w-6" />
              </div>
              <div>
                <p className="font-semibold text-base flex items-center gap-1.5">
                  Công thức chưa có ảnh bìa
                  <span className="inline-flex items-center rounded-full bg-amber-400/20 px-2 py-0.5 text-xs text-amber-300">
                    <Sparkles className="h-3 w-3 mr-1" /> Mới
                  </span>
                </p>
                <p className="text-xs text-white/80">
                  Tải ảnh từ máy tính lên để công thức hấp dẫn và sinh động hơn.
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="primary"
                size="sm"
                isLoading={isUploading}
                onClick={() => fileInputRef.current?.click()}
                className="bg-brand-600 hover:bg-brand-500 shadow-md"
              >
                <ImagePlus className="h-4 w-4 mr-1.5" />
                Tải ảnh từ máy tính
              </Button>

              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setIsOpen(true)}
                className="border-white/30 text-white hover:bg-white/10"
              >
                Chỉnh sửa ảnh
              </Button>
            </div>
          </div>

          {error && (
            <p className="mt-3 rounded-lg bg-spice-600/80 px-3 py-1.5 text-xs text-white">
              {error}
            </p>
          )}
        </div>
      )}

      {/* 2. Nếu công thức ĐÃ có ảnh: hiển thị nút chỉnh sửa ảnh tiện lợi */}
      {hasImages && (
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={isUploading}
            className="inline-flex items-center gap-1.5 rounded-full border border-white/30 bg-black/40 px-3 py-1.5 text-xs font-medium text-white backdrop-blur-md transition-colors hover:bg-black/60 disabled:opacity-50"
          >
            {isUploading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <ImagePlus className="h-3.5 w-3.5" />}
            Tải thêm ảnh từ máy
          </button>

          <button
            type="button"
            onClick={() => setIsOpen(true)}
            className="inline-flex items-center gap-1.5 rounded-full border border-white/30 bg-black/40 px-3 py-1.5 text-xs font-medium text-white backdrop-blur-md transition-colors hover:bg-black/60"
          >
            <Camera className="h-3.5 w-3.5" />
            Quản lý & Chỉnh sửa ảnh ({recipe.images.length})
          </button>
        </div>
      )}

      {/* 3. Modal render qua createPortal ra document.body */}
      {modal}
    </>
  );
}


