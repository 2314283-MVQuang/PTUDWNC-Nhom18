"use client";

import Image from "next/image";
import { useRef, useState } from "react";
import { Camera, ImagePlus, Images, Upload, X } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { apiErrorMessage } from "@/lib/api-client";
import { useMyImages, useUploadImageFile } from "@/hooks/useRecipeParts";
import { ACCEPTED_IMAGE_TYPES, StorageUploadError, validateImageFile } from "@/lib/recipe-images";
import { cn, resolveMediaUrl } from "@/lib/utils";
import type { MyImage } from "@/types/media";

const ACCEPTED_TYPES = ACCEPTED_IMAGE_TYPES.join(",");

/**
 * Tuần 10 (Quang) — ô chọn MỘT ảnh, dùng cho ảnh minh hoạ từng bước (FR-RCP-010: form tạo công
 * thức + trang sửa) và ảnh danh mục (FR-CAT-003/004). Bốn cách lấy ảnh:
 *   1. "Tải từ máy": hộp chọn file (trên điện thoại trình duyệt hỏi Thư viện ảnh / Tệp);
 *   2. "Chụp ảnh": mở camera sau trên điện thoại (`capture="environment"`); máy tính → chọn file;
 *   3. kéo thả ảnh vào khung;
 *   4. "Thư viện của tôi": chọn lại ảnh đã tải lên trước đó (GET /files/mine) — tải lên bản sao mới.
 *
 * Ảnh được tải ngay lên MinIO (presigned URL) và trả URL cho form cha qua `onChange`; chỉ thật sự
 * gắn khi form cha lưu. Backend kiểm tra lại URL (RecipeImageUploadGuard).
 */
export function ImagePickerField({
  value,
  onChange,
  alt,
  disabled = false,
  emptyLabel = "Thêm ảnh minh hoạ",
}: {
  value: string | null;
  onChange: (url: string | null) => void;
  alt: string;
  disabled?: boolean;
  emptyLabel?: string;
}) {
  const upload = useUploadImageFile();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const cameraInputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [libraryOpen, setLibraryOpen] = useState(false);
  const busy = disabled || upload.isPending;
  const src = resolveMediaUrl(value);

  async function applySource(source: File | MyImage) {
    setError(null);
    if (source instanceof File) {
      const invalid = validateImageFile(source);
      if (invalid) {
        setError(invalid);
        return;
      }
    }
    try {
      onChange(await upload.mutateAsync(source));
    } catch (err) {
      setError(err instanceof StorageUploadError ? err.message : apiErrorMessage(err, "Không tải được ảnh lên."));
    }
  }

  function handleInputChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    // Xoá giá trị để chọn lại đúng file vừa rồi vẫn kích hoạt onChange.
    event.target.value = "";
    if (file) void applySource(file);
  }

  function handleDrop(event: React.DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDragging(false);
    if (busy) return;
    const file = event.dataTransfer.files?.[0];
    if (file) void applySource(file);
  }

  return (
    <div className="flex flex-col gap-2">
      <input ref={fileInputRef} type="file" accept={ACCEPTED_TYPES} className="hidden" onChange={handleInputChange} />
      <input
        ref={cameraInputRef}
        type="file"
        accept="image/*"
        capture="environment"
        className="hidden"
        onChange={handleInputChange}
      />

      <div
        onDragOver={(event) => {
          event.preventDefault();
          if (!busy) setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={handleDrop}
        className={cn(
          "flex flex-col gap-3 rounded-card border border-dashed p-3 transition-colors sm:flex-row sm:items-center",
          isDragging ? "border-brand-500 bg-brand-50" : "border-neutral-300",
        )}
      >
        {src ? (
          <div className="relative aspect-video w-full shrink-0 overflow-hidden rounded-lg border border-neutral-200 bg-neutral-100 sm:w-40">
            <Image src={src} alt={alt} fill sizes="(max-width: 640px) 100vw, 160px" className="object-cover" unoptimized />
          </div>
        ) : (
          <div className="flex items-center gap-2 text-sm text-neutral-500">
            <ImagePlus className="h-5 w-5 shrink-0" />
            <span>
              {emptyLabel} — kéo thả ảnh vào đây hoặc chọn bên dưới.
              <span className="block text-xs">JPEG, PNG, WebP hoặc AVIF, tối đa 5MB.</span>
            </span>
          </div>
        )}

        <div className="flex flex-wrap gap-2 sm:ml-auto">
          <Button type="button" size="sm" variant="outline" disabled={busy} isLoading={upload.isPending} onClick={() => fileInputRef.current?.click()}>
            <Upload className="h-4 w-4" />
            {src ? "Đổi ảnh" : "Tải từ máy"}
          </Button>
          <Button type="button" size="sm" variant="outline" disabled={busy} onClick={() => cameraInputRef.current?.click()}>
            <Camera className="h-4 w-4" />
            Chụp ảnh
          </Button>
          <Dialog
            open={libraryOpen}
            onOpenChange={setLibraryOpen}
            title="Thư viện ảnh của tôi"
            description="Ảnh bạn đã tải lên trước đây. Chọn một ảnh để dùng lại — hệ thống tạo bản sao riêng cho chỗ này."
            trigger={
              <Button type="button" size="sm" variant="outline" disabled={busy}>
                <Images className="h-4 w-4" />
                Thư viện của tôi
              </Button>
            }
          >
            <MyImageLibrary
              open={libraryOpen}
              onPick={(image) => {
                setLibraryOpen(false);
                void applySource(image);
              }}
            />
          </Dialog>
          {src && (
            <Button type="button" size="sm" variant="ghost" disabled={busy} onClick={() => onChange(null)}>
              <X className="h-4 w-4" />
              Gỡ ảnh
            </Button>
          )}
        </div>
      </div>

      {error && <p className="text-sm text-spice-600">{error}</p>}
    </div>
  );
}

function MyImageLibrary({ open, onPick }: { open: boolean; onPick: (image: MyImage) => void }) {
  const library = useMyImages(open);
  // Ảnh tải lên trước khi có cơ chế đánh dấu file đã xoá có thể không còn trên MinIO → ẩn đi.
  const [broken, setBroken] = useState<Set<string>>(new Set());
  const images = (library.data?.pages.flatMap((page) => page.items) ?? []).filter((image) => !broken.has(image.id));

  if (library.isPending) return <div className="skeleton h-40 w-full" />;
  if (library.isError) {
    return <p className="text-sm text-spice-600">{apiErrorMessage(library.error, "Không tải được thư viện ảnh.")}</p>;
  }
  if (images.length === 0) {
    return <p className="text-sm text-neutral-500">Bạn chưa tải ảnh nào lên. Hãy dùng “Tải từ máy” hoặc “Chụp ảnh”.</p>;
  }

  return (
    <div className="flex flex-col gap-3">
      <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4">
        {images.map((image) => {
          const src = resolveMediaUrl(image.thumbnailUrl ?? image.url);
          return (
            <li key={image.id}>
              <button
                type="button"
                onClick={() => onPick(image)}
                className="relative block aspect-square w-full overflow-hidden rounded-lg border border-neutral-200 bg-neutral-100 hover:ring-2 hover:ring-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
                aria-label={`Chọn ảnh ${image.fileName}`}
                title={image.fileName}
              >
                {src && (
                  <Image
                    src={src}
                    alt={image.fileName}
                    fill
                    sizes="120px"
                    className="object-cover"
                    unoptimized
                    onError={() => setBroken((current) => new Set(current).add(image.id))}
                  />
                )}
              </button>
            </li>
          );
        })}
      </ul>
      {library.hasNextPage && (
        <Button type="button" size="sm" variant="ghost" isLoading={library.isFetchingNextPage} onClick={() => library.fetchNextPage()}>
          Xem thêm
        </Button>
      )}
    </div>
  );
}
