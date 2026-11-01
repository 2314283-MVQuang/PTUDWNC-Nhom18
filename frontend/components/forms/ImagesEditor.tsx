"use client";

import Image from "next/image";
import { useEffect, useRef, useState } from "react";
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

/** Lấy tên file cuối cùng trong URL (dùng để phát hiện trùng lặp). */
function getFilenameFromUrl(url: string): string {
  try { return new URL(url).pathname.split("/").pop() ?? url; } catch { return url.split("/").pop() ?? url; }
}

/**
 * FR-RCP-008 — upload (presigned URL MinIO / direct fallback), đặt ảnh đại diện, xoá ảnh.
 *
 * Quản lý danh sách ảnh mượt mà, cập nhật trực quan ngay trên giao diện khi thêm, xóa, đổi ảnh đại diện.
 * Phát hiện ảnh trùng (cùng tên file) và thông báo trước khi upload.
 */
export function ImagesEditor({ recipeId, images }: { recipeId: string; images: RecipeImage[] }) {
  const uploadImage = useUploadImage(recipeId);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [items, setItems] = useState<RecipeImage[]>(images);

  useEffect(() => {
    setItems(images);
  }, [images]);

  const sorted = items.slice().sort((a, b) => a.orderIndex - b.orderIndex);

  async function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []);
    if (files.length === 0) return;

    setError(null);
    setInfo(null);

    const uploadedNew: RecipeImage[] = [];
    const skippedNames: string[] = [];

    for (const file of files) {
      const invalid = validateImageFile(file);
      if (invalid) {
        setError(invalid);
        continue;
      }

      // Kiểm tra trùng tên file với ảnh đã có hoặc vừa tải
      const existingNames = [...items, ...uploadedNew].map((img) =>
        getFilenameFromUrl(img.originalUrl ?? img.thumbnailUrl ?? "").toLowerCase()
      );
      const uploadName = file.name.toLowerCase();
      if (existingNames.some((n) => n === uploadName || n.startsWith(uploadName.replace(/\.[^.]+$/, "")))) {
        skippedNames.push(file.name);
        continue;
      }

      try {
        const newImg = await uploadImage.mutateAsync({ file });
        if (newImg) {
          uploadedNew.push(newImg);
        }
      } catch (err) {
        setError(err instanceof StorageUploadError ? err.message : apiErrorMessage(err, "Không upload được ảnh."));
      }
    }

    if (uploadedNew.length > 0) {
      setItems((prev) => {
        const combined = [...prev, ...uploadedNew];
        if (!combined.some((img) => img.isPrimary) && combined.length > 0) {
          combined[0] = { ...combined[0], isPrimary: true };
        }
        return combined;
      });
    }

    if (skippedNames.length > 0) {
      setInfo(`Đã bỏ qua ${skippedNames.length} ảnh do trùng tên file: ${skippedNames.join(", ")}`);
    }

    event.target.value = "";
  }

  function handleImageDeleted(id: string) {
    setItems((prev) => {
      const filtered = prev.filter((img) => img.id !== id);
      if (filtered.length > 0 && !filtered.some((img) => img.isPrimary)) {
        filtered[0] = { ...filtered[0], isPrimary: true };
      }
      return filtered;
    });
  }

  function handleImagePrimarySet(id: string) {
    setItems((prev) =>
      prev.map((img) => ({
        ...img,
        isPrimary: img.id === id,
      }))
    );
  }

  return (
    <div className="flex flex-col gap-3">
      {sorted.length === 0 ? (
        <div
          onClick={() => fileInputRef.current?.click()}
          className="flex cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed border-neutral-300 bg-neutral-50/80 p-8 text-center transition-colors hover:border-brand-500 hover:bg-brand-50/30 group"
        >
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-100 text-brand-600 group-hover:scale-110 transition-transform">
            <ImageUp className="h-6 w-6" />
          </div>
          <p className="mt-3 text-sm font-semibold text-neutral-800">
            Nhấn vào đây để tải ảnh từ máy tính
          </p>
          <p className="mt-1 text-xs text-neutral-500">
            JPEG, PNG, WebP hoặc GIF (tối đa 10MB). Ảnh đầu tiên sẽ tự động làm ảnh đại diện.
          </p>
        </div>
      ) : (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {sorted.map((image) => (
            <ImageCard
              key={image.id}
              recipeId={recipeId}
              image={image}
              onDeleted={() => handleImageDeleted(image.id)}
              onPrimarySet={() => handleImagePrimarySet(image.id)}
            />
          ))}
        </ul>
      )}

      <input
        ref={fileInputRef}
        type="file"
        accept={ACCEPTED_TYPES}
        multiple
        className="hidden"
        onChange={handleFileChange}
      />

      {error && (
        <p className="rounded-lg bg-spice-50 px-3 py-2 text-sm text-spice-700">{error}</p>
      )}

      {info && (
        <p className="rounded-lg bg-blue-50 px-3 py-2 text-sm text-blue-700">{info}</p>
      )}

      <div>
        <Button
          type="button"
          size="sm"
          variant="outline"
          isLoading={uploadImage.isPending}
          onClick={() => fileInputRef.current?.click()}
        >
          <ImageUp className="h-4 w-4 mr-1.5" />
          Tải ảnh lên từ máy
        </Button>
        <p className="mt-1 text-xs text-neutral-500">
          JPEG, PNG, WebP hoặc GIF, tối đa 10MB. Ảnh đầu tiên tự động thành ảnh đại diện.
        </p>
      </div>
    </div>
  );
}

function ImageCard({
  recipeId,
  image,
  onDeleted,
  onPrimarySet,
}: {
  recipeId: string;
  image: RecipeImage;
  onDeleted?: () => void;
  onPrimarySet?: () => void;
}) {
  const setPrimary = useSetPrimaryImage(recipeId);
  const deleteImage = useDeleteImage(recipeId);
  const [error, setError] = useState<string | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  const src = resolveMediaUrl(image.thumbnailUrl ?? image.originalUrl);

  async function handleSetPrimary() {
    setError(null);
    try {
      await setPrimary.mutateAsync(image.id);
      onPrimarySet?.();
    } catch (err) {
      setError(apiErrorMessage(err, "Không đặt được ảnh đại diện."));
    }
  }

  async function handleDelete() {
    setError(null);
    try {
      await deleteImage.mutateAsync(image.id);
    } catch (err: unknown) {
      const errorObj = err as { status?: number; message?: string };
      const msg = errorObj?.message ?? "";
      const status = errorObj?.status;
      if (status !== 404 && !msg.includes("Không tìm thấy")) {
        setError(apiErrorMessage(err, "Không xoá được ảnh."));
        return;
      }
    }
    setDeleteDialogOpen(false);
    onDeleted?.();
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
            unoptimized
          />
        )}

        {image.isPrimary && (
          <span className="absolute left-1.5 top-1.5 inline-flex items-center gap-1 rounded-full bg-brand-600 px-2 py-0.5 text-xs font-medium text-white shadow">
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
