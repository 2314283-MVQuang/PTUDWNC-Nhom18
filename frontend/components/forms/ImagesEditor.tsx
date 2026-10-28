"use client";

import Image from "next/image";
import { useRef, useState } from "react";
import { ImageUp, Star, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { apiErrorMessage } from "@/lib/api-client";
import { useDeleteImage, useSetPrimaryImage, useUploadImage } from "@/hooks/useRecipeParts";
import { ACCEPTED_IMAGE_TYPES, StorageUploadError, validateImageFile } from "@/lib/recipe-images";
import { resolveMediaUrl } from "@/lib/utils";
import type { RecipeImage } from "@/types/recipe";

/** Khớp danh sách loại ảnh backend cho phép khi cấp presigned URL (FR-FILE-001). */
const ACCEPTED_TYPES = ACCEPTED_IMAGE_TYPES.join(",");

/**
 * FR-RCP-008 — upload (presigned URL MinIO), đặt ảnh đại diện, xoá ảnh.
 *
 * Thông báo lỗi trả về (sai loại, quá dung lượng, MinIO không phản hồi) được hiển thị nguyên văn
 * cho người dùng, đừng thay bằng câu chung chung — đó là lúc duy nhất họ biết vì sao ảnh không lên.
 */
export function ImagesEditor({ recipeId, images }: { recipeId: string; images: RecipeImage[] }) {
  const uploadImage = useUploadImage(recipeId);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string | null>(null);

  const sorted = images.slice().sort((a, b) => a.orderIndex - b.orderIndex);

  async function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;

    setError(null);
    const invalid = validateImageFile(file);
    if (invalid) {
      setError(invalid);
      event.target.value = "";
      return;
    }
    try {
      await uploadImage.mutateAsync({ file });
    } catch (err) {
      setError(err instanceof StorageUploadError ? err.message : apiErrorMessage(err, "Không upload được ảnh."));
    } finally {
      // Xoá giá trị input để chọn lại ĐÚNG file vừa rồi vẫn kích hoạt onChange (trình duyệt bỏ
      // qua sự kiện khi giá trị không đổi) — cần khi người dùng sửa file rồi thử lại.
      event.target.value = "";
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {sorted.length === 0 && (
        <p className="text-sm text-neutral-500">Chưa có ảnh nào cho công thức này.</p>
      )}

      {sorted.length > 0 && (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {sorted.map((image) => (
            <ImageCard key={image.id} recipeId={recipeId} image={image} />
          ))}
        </ul>
      )}

      <input
        ref={fileInputRef}
        type="file"
        accept={ACCEPTED_TYPES}
        className="hidden"
        onChange={handleFileChange}
      />

      {error && (
        <p className="rounded-lg bg-spice-50 px-3 py-2 text-sm text-spice-700">{error}</p>
      )}

      <div>
        <Button
          type="button"
          size="sm"
          variant="outline"
          isLoading={uploadImage.isPending}
          onClick={() => fileInputRef.current?.click()}
        >
          <ImageUp className="h-4 w-4" />
          Tải ảnh lên
        </Button>
        <p className="mt-1 text-xs text-neutral-500">
          JPEG, PNG, WebP hoặc GIF, tối đa 10MB. Ảnh đầu tiên tự động thành ảnh đại diện.
        </p>
      </div>
    </div>
  );
}

function ImageCard({ recipeId, image }: { recipeId: string; image: RecipeImage }) {
  const setPrimary = useSetPrimaryImage(recipeId);
  const deleteImage = useDeleteImage(recipeId);
  const [error, setError] = useState<string | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  const src = resolveMediaUrl(image.thumbnailUrl ?? image.originalUrl);

  async function handleSetPrimary() {
    setError(null);
    try {
      await setPrimary.mutateAsync(image.id);
    } catch (err) {
      setError(apiErrorMessage(err, "Không đặt được ảnh đại diện."));
    }
  }

  async function handleDelete() {
    setError(null);
    try {
      await deleteImage.mutateAsync(image.id);
      setDeleteDialogOpen(false);
    } catch (err) {
      setError(apiErrorMessage(err, "Không xoá được ảnh."));
    }
  }

  return (
    <li className="flex flex-col gap-1">
      <div className="relative aspect-square overflow-hidden rounded-card border border-neutral-200 bg-neutral-100">
        {src && (
          <Image
            src={src}
            alt={image.altText ?? "Ảnh công thức"}
            fill
            sizes="(max-width: 640px) 50vw, 25vw"
            className="object-cover"
            // Ảnh do backend .NET phục vụ tĩnh, không qua trình tối ưu của Next.js.
            unoptimized
          />
        )}

        {image.isPrimary && (
          <span className="absolute left-1.5 top-1.5 inline-flex items-center gap-1 rounded-full bg-brand-600 px-2 py-0.5 text-xs font-medium text-white">
            <Star className="h-3 w-3 fill-current" />
            Đại diện
          </span>
        )}
      </div>

      <div className="flex items-center justify-between gap-1">
        {image.isPrimary ? (
          <span className="text-xs text-neutral-400">Ảnh đại diện</span>
        ) : (
          <button
            type="button"
            disabled={setPrimary.isPending}
            className="text-xs font-medium text-brand-700 hover:underline disabled:opacity-50"
            onClick={handleSetPrimary}
          >
            Đặt làm đại diện
          </button>
        )}

        <Dialog
          open={deleteDialogOpen}
          onOpenChange={(open) => {
            if (deleteImage.isPending && !open) return;
            setDeleteDialogOpen(open);
            if (open) setError(null);
          }}
          title="Xác nhận xóa ảnh"
          description="Bạn có chắc chắn muốn xóa ảnh này khỏi công thức không? Hành động này không thể hoàn tác."
          trigger={
            <button
              type="button"
              aria-label="Xoá ảnh"
              disabled={deleteImage.isPending}
              className="rounded-lg p-1 text-neutral-500 hover:bg-spice-50 hover:text-spice-600 disabled:opacity-50"
            >
              <Trash2 className="h-4 w-4" />
            </button>
          }
        >
          {error && (
            <p role="alert" className="mb-4 rounded-lg bg-spice-50 px-3 py-2 text-sm text-spice-700">
              {error}
            </p>
          )}
          <div className="flex justify-end gap-3">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={deleteImage.isPending}
              className="border-neutral-200 bg-neutral-100 text-neutral-700 hover:border-neutral-300 hover:bg-neutral-200 hover:text-neutral-900"
              onClick={() => setDeleteDialogOpen(false)}
            >
              Hủy
            </Button>
            <Button
              type="button"
              variant="danger"
              size="sm"
              isLoading={deleteImage.isPending}
              onClick={handleDelete}
            >
              Xóa ảnh
            </Button>
          </div>
        </Dialog>
      </div>

      {error && <p className="text-xs text-spice-600">{error}</p>}
    </li>
  );
}
