"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Controller, useForm } from "react-hook-form";
import { Button } from "@/components/ui/Button";
import { Input, Textarea } from "@/components/ui/Input";
import { categorySchema, type CategoryFormValues } from "@/lib/validations/recipe.schema";
import type { Category } from "@/types/category";
import { ImagePickerField } from "./ImagePickerField";

/** Form tạo/sửa category (FR-CAT-003, FR-CAT-004) — dùng chung cho cả 2 trường hợp qua prop `initialData`. */
export function CategoryForm({
  initialData,
  onSubmit,
  onCancel,
  isSubmitting,
}: {
  initialData?: Category;
  onSubmit: (values: CategoryFormValues) => void;
  onCancel?: () => void;
  isSubmitting?: boolean;
}) {
  const {
    register,
    control,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: {
      name: initialData?.name ?? "",
      description: initialData?.description ?? "",
      // Tuần 10: giữ ảnh hiện tại — PUT /categories/{id} thiếu imageUrl sẽ gỡ ảnh.
      imageUrl: initialData?.imageUrl ?? null,
    },
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
      <Input label="Tên danh mục" error={errors.name?.message} {...register("name")} />
      <Textarea label="Mô tả" error={errors.description?.message} {...register("description")} />
      <div className="flex flex-col gap-1.5">
        <span className="text-sm font-medium text-neutral-700">Ảnh danh mục</span>
        <Controller
          control={control}
          name="imageUrl"
          render={({ field }) => (
            <ImagePickerField
              value={field.value ?? null}
              onChange={field.onChange}
              alt={watch("name") || "Ảnh danh mục"}
              disabled={isSubmitting}
              emptyLabel="Thêm ảnh đại diện cho danh mục"
            />
          )}
        />
      </div>
      {/* Slug tự sinh ở backend từ Name (FR-CAT-003) — không có field nhập tay ở đây. */}

      <div className="flex justify-end gap-2">
        {onCancel && (
          <Button type="button" variant="ghost" onClick={onCancel}>
            Huỷ
          </Button>
        )}
        <Button type="submit" isLoading={isSubmitting}>
          {initialData ? "Lưu thay đổi" : "Tạo danh mục"}
        </Button>
      </div>
    </form>
  );
}
