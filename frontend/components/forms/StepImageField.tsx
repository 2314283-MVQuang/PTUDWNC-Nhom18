"use client";

import Image from "next/image";
import { useRef, useState } from "react";
import { ImagePlus, X } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { apiErrorMessage } from "@/lib/api-client";
import { useUploadImageFile } from "@/hooks/useRecipeParts";
import { ACCEPTED_IMAGE_TYPES, StorageUploadError, validateImageFile } from "@/lib/recipe-images";
import { resolveMediaUrl } from "@/lib/utils";

const ACCEPTED_TYPES = ACCEPTED_IMAGE_TYPES.join(",");

/**
 * FR-RCP-010 (Tuần 10 — Quang): ô chọn ảnh minh hoạ cho MỘT bước thực hiện, dùng ở form tạo
 * công thức (RecipeWizardForm) và trang sửa (StepsEditor).
 *
 * Chọn file → tải ngay lên MinIO qua presigned URL → trả URL cho form cha qua `onChange`. Ảnh chỉ
 * thật sự gắn vào bước khi form cha lưu bước/công thức; backend kiểm tra lại URL (phải do chính
 * người dùng tải lên, đúng là ảnh, ≤ 5MB, chưa dùng ở chỗ khác).
 */
export function StepImageField({
  value,
  onChange,
  alt,
  disabled = false,
}: {
  value: string | null;
  onChange: (url: string | null) => void;
  alt: string;
  disabled?: boolean;
}) {
  const upload = useUploadImageFile();
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string | null>(null);
  const src = resolveMediaUrl(value);

  async function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    // Xoá giá trị để chọn lại đúng file vừa rồi vẫn kích hoạt onChange.
    event.target.value = "";
    if (!file) return;

    setError(null);
    const invalid = validateImageFile(file);
    if (invalid) {
      setError(invalid);
      return;
    }
    try {
      onChange(await upload.mutateAsync(file));
    } catch (err) {
      setError(err instanceof StorageUploadError ? err.message : apiErrorMessage(err, "Không tải được ảnh lên."));
    }
  }

  return (
    <div className="flex flex-col gap-1.5">
      <input ref={inputRef} type="file" accept={ACCEPTED_TYPES} className="hidden" onChange={handleFileChange} />

      {src ? (
        <div className="flex items-end gap-2">
          <div className="relative aspect-video w-40 overflow-hidden rounded-lg border border-neutral-200 bg-neutral-100">
            <Image src={src} alt={alt} fill sizes="160px" className="object-cover" unoptimized />
          </div>
          <div className="flex flex-col gap-1">
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={disabled}
              isLoading={upload.isPending}
              onClick={() => inputRef.current?.click()}
            >
              <ImagePlus className="h-4 w-4" />
              Đổi ảnh
            </Button>
            <Button
              type="button"
              size="sm"
              variant="ghost"
              disabled={disabled || upload.isPending}
              onClick={() => onChange(null)}
            >
              <X className="h-4 w-4" />
              Gỡ ảnh
            </Button>
          </div>
        </div>
      ) : (
        <div>
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={disabled}
            isLoading={upload.isPending}
            onClick={() => inputRef.current?.click()}
          >
            <ImagePlus className="h-4 w-4" />
            Thêm ảnh minh hoạ
          </Button>
          <p className="mt-1 text-xs text-neutral-500">Tuỳ chọn · JPEG, PNG, WebP hoặc AVIF, tối đa 5MB.</p>
        </div>
      )}

      {error && <p className="text-sm text-spice-600">{error}</p>}
    </div>
  );
}
