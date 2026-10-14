"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Trash2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Input, Select, Textarea } from "@/components/ui/Input";
import { useCategories } from "@/hooks/useCategories";
import { useCreateRecipe } from "@/hooks/useRecipes";
import {
  recipeBasicInfoSchema,
  recipeIngredientSchema,
  recipeStepSchema,
  type RecipeBasicInfoValues,
  type RecipeIngredientValues,
  type RecipeStepValues,
} from "@/lib/validations/recipe.schema";
import { DIFFICULTY_LABEL } from "@/lib/utils";
import type { CreateRecipeInput } from "@/types/recipe";

type WizardStep = 1 | 2 | 3 | 4;

/**
 * Form tạo recipe mới dạng "multi-step wizard" (mục 9: /dashboard/recipes/new).
 * 4 bước: Thông tin cơ bản -> Nguyên liệu -> Các bước thực hiện -> Xem lại & Lưu.
 * Toàn bộ dữ liệu chỉ thật sự gửi lên backend 1 lần ở bước cuối (FR-RCP-003: có thể
 * tạo Steps/Ingredients cùng lúc trong request), tránh tạo Draft rỗng rồi phải sửa nhiều lần.
 */
export function RecipeWizardForm() {
  const router = useRouter();
  const [step, setStep] = useState<WizardStep>(1);
  const [basicInfo, setBasicInfo] = useState<RecipeBasicInfoValues | null>(null);
  const [ingredients, setIngredients] = useState<RecipeIngredientValues[]>([]);
  const [steps, setSteps] = useState<RecipeStepValues[]>([]);

  const createRecipe = useCreateRecipe();

  async function handleFinalSubmit() {
    if (!basicInfo) return;
    const input: CreateRecipeInput = {
      title: basicInfo.title,
      description: basicInfo.description,
      categoryId: basicInfo.categoryId,
      prepTime: basicInfo.prepTimeMinutes,
      cookTime: basicInfo.cookTimeMinutes,
      servings: basicInfo.servings,
      difficulty: basicInfo.difficulty,
      // Zod trả về `undefined` cho field bỏ trống, còn DTO của backend (mục 7.3/7.4) dùng `null`.
      // Đổi undefined -> null ở đây để JSON gửi đi đúng chuẩn API, không phải bỏ hẳn key.
      ingredients: ingredients.map((item, index) => ({
        name: item.name,
        quantity: item.quantity ?? null,
        unit: item.unit ?? null,
        notes: item.notes ?? null,
        // OrderIndex đếm từ 0, khớp DEFAULT 0 của cột trong db/init/02-schema.sql.
        orderIndex: index,
      })),
      steps: steps.map((step, index) => ({
        // StepNumber đếm từ 1 và validator bắt buộc > 0 — xem CreateRecipeStepInput ở backend.
        stepNumber: index + 1,
        title: step.title,
        description: step.description,
        timerMinutes: step.timerMinutes ?? null,
      })),
    };
    const created = await createRecipe.mutateAsync(input);
    // Trang chỉnh sửa hiện dùng slug làm định danh trên URL.
    router.push(`/dashboard/recipes/${created.slug}/edit`);
  }

  return (
    <div className="flex flex-col gap-6">
      <WizardStepper current={step} />

      {step === 1 && (
        <BasicInfoStep
          defaultValues={basicInfo ?? undefined}
          onNext={(values) => {
            setBasicInfo(values);
            setStep(2);
          }}
        />
      )}

      {step === 2 && (
        <IngredientsStep
          ingredients={ingredients}
          onChange={setIngredients}
          onBack={() => setStep(1)}
          onNext={() => setStep(3)}
        />
      )}

      {step === 3 && (
        <StepsStep steps={steps} onChange={setSteps} onBack={() => setStep(2)} onNext={() => setStep(4)} />
      )}

      {step === 4 && basicInfo && (
        <ReviewStep
          basicInfo={basicInfo}
          ingredients={ingredients}
          steps={steps}
          onBack={() => setStep(3)}
          onSubmit={handleFinalSubmit}
          isSubmitting={createRecipe.isPending}
          error={createRecipe.isError ? "Không thể tạo công thức, vui lòng thử lại." : null}
        />
      )}
    </div>
  );
}

function WizardStepper({ current }: { current: WizardStep }) {
  const labels = ["Thông tin cơ bản", "Nguyên liệu", "Các bước thực hiện", "Xem lại & Lưu"];
  return (
    <ol className="flex flex-wrap gap-4 text-sm">
      {labels.map((label, i) => {
        const stepNumber = (i + 1) as WizardStep;
        const active = stepNumber === current;
        const done = stepNumber < current;
        return (
          <li key={label} className={done ? "text-brand-600" : active ? "font-semibold text-neutral-900" : "text-neutral-400"}>
            {i + 1}. {label}
          </li>
        );
      })}
    </ol>
  );
}

/** Bước 1 — dùng chung schema với trang edit (recipeBasicInfoSchema). */
function BasicInfoStep({
  defaultValues,
  onNext,
}: {
  defaultValues?: RecipeBasicInfoValues;
  onNext: (values: RecipeBasicInfoValues) => void;
}) {
  const { data: categories } = useCategories();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RecipeBasicInfoValues>({ resolver: zodResolver(recipeBasicInfoSchema), defaultValues });

  return (
    <form onSubmit={handleSubmit(onNext)} className="flex flex-col gap-4">
      <Input label="Tên công thức" error={errors.title?.message} {...register("title")} />
      <Textarea label="Mô tả ngắn" error={errors.description?.message} {...register("description")} />
      <Select
        label="Danh mục"
        placeholder="Chọn danh mục"
        error={errors.categoryId?.message}
        options={(categories ?? []).map((c) => ({ value: c.id, label: c.name }))}
        {...register("categoryId")}
      />
      <div className="grid grid-cols-3 gap-4">
        <Input label="Chuẩn bị (phút)" type="number" error={errors.prepTimeMinutes?.message} {...register("prepTimeMinutes")} />
        <Input label="Thời gian nấu (phút)" type="number" error={errors.cookTimeMinutes?.message} {...register("cookTimeMinutes")} />
        <Input label="Khẩu phần" type="number" error={errors.servings?.message} {...register("servings")} />
      </div>
      <Select
        label="Độ khó"
        options={Object.entries(DIFFICULTY_LABEL).map(([value, label]) => ({ value, label }))}
        error={errors.difficulty?.message}
        {...register("difficulty")}
      />
      <div className="flex justify-end">
        <Button type="submit">Tiếp theo</Button>
      </div>
    </form>
  );
}

/** Bước 2 — thêm nguyên liệu vào 1 danh sách tạm trước khi lưu (FR-RCP-009). */
function IngredientsStep({
  ingredients,
  onChange,
  onBack,
  onNext,
}: {
  ingredients: RecipeIngredientValues[];
  onChange: (items: RecipeIngredientValues[]) => void;
  onBack: () => void;
  onNext: () => void;
}) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<RecipeIngredientValues>({ resolver: zodResolver(recipeIngredientSchema) });

  function addIngredient(values: RecipeIngredientValues) {
    onChange([...ingredients, values]);
    reset({ name: "", quantity: undefined, unit: "", notes: "" });
  }

  return (
    <div className="flex flex-col gap-4">
      <form onSubmit={handleSubmit(addIngredient)} className="grid grid-cols-1 gap-3 sm:grid-cols-[2fr_1fr_1fr_auto]">
        <Input placeholder="Tên nguyên liệu" error={errors.name?.message} {...register("name")} />
        <Input placeholder="Số lượng" type="number" step="any" {...register("quantity")} />
        <Input placeholder="Đơn vị (gram, ml...)" {...register("unit")} />
        <Button type="submit" variant="outline">
          Thêm
        </Button>
      </form>

      <ul className="divide-y divide-neutral-100 rounded-card border border-neutral-200">
        {ingredients.length === 0 && <li className="p-4 text-sm text-neutral-400">Chưa có nguyên liệu nào.</li>}
        {ingredients.map((ing, index) => (
          <li key={index} className="flex items-center justify-between gap-2 p-3 text-sm">
            <span>
              {ing.name} — {ing.quantity ?? ""} {ing.unit ?? ""}
            </span>
            <button
              type="button"
              onClick={() => onChange(ingredients.filter((_, i) => i !== index))}
              className="text-neutral-400 hover:text-red-600"
              aria-label="Xoá nguyên liệu"
            >
              <Trash2 className="h-4 w-4" />
            </button>
          </li>
        ))}
      </ul>

      <div className="flex justify-between">
        <Button type="button" variant="ghost" onClick={onBack}>
          Quay lại
        </Button>
        <Button type="button" onClick={onNext}>
          Tiếp theo
        </Button>
      </div>
    </div>
  );
}

/** Bước 3 — thêm các bước thực hiện, StepNumber tự tính theo thứ tự thêm vào (FR-RCP-010). */
function StepsStep({
  steps,
  onChange,
  onBack,
  onNext,
}: {
  steps: RecipeStepValues[];
  onChange: (items: RecipeStepValues[]) => void;
  onBack: () => void;
  onNext: () => void;
}) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<RecipeStepValues>({ resolver: zodResolver(recipeStepSchema) });

  function addStep(values: RecipeStepValues) {
    onChange([...steps, values]);
    reset({ title: "", description: "", timerMinutes: undefined });
  }

  return (
    <div className="flex flex-col gap-4">
      <form onSubmit={handleSubmit(addStep)} className="flex flex-col gap-3 rounded-card border border-neutral-200 p-4">
        <Input placeholder="Tiêu đề bước" error={errors.title?.message} {...register("title")} />
        <Textarea placeholder="Mô tả chi tiết" error={errors.description?.message} {...register("description")} />
        <div className="flex items-center gap-3">
          <Input placeholder="Thời gian (phút, tuỳ chọn)" type="number" className="max-w-xs" {...register("timerMinutes")} />
          <Button type="submit" variant="outline">
            Thêm bước
          </Button>
        </div>
      </form>

      <ol className="flex flex-col gap-2">
        {steps.length === 0 && <p className="text-sm text-neutral-400">Chưa có bước nào — không thể publish nếu thiếu (FR-RCP-005).</p>}
        {steps.map((s, index) => (
          <li key={index} className="flex items-start justify-between gap-2 rounded-card border border-neutral-200 p-3 text-sm">
            <div>
              <span className="font-medium">
                {index + 1}. {s.title}
              </span>
              <p className="text-neutral-500">{s.description}</p>
            </div>
            <button
              type="button"
              onClick={() => onChange(steps.filter((_, i) => i !== index))}
              className="text-neutral-400 hover:text-red-600"
              aria-label="Xoá bước"
            >
              <Trash2 className="h-4 w-4" />
            </button>
          </li>
        ))}
      </ol>

      <div className="flex justify-between">
        <Button type="button" variant="ghost" onClick={onBack}>
          Quay lại
        </Button>
        <Button type="button" onClick={onNext}>
          Tiếp theo
        </Button>
      </div>
    </div>
  );
}

function ReviewStep({
  basicInfo,
  ingredients,
  steps,
  onBack,
  onSubmit,
  isSubmitting,
  error,
}: {
  basicInfo: RecipeBasicInfoValues;
  ingredients: RecipeIngredientValues[];
  steps: RecipeStepValues[];
  onBack: () => void;
  onSubmit: () => void;
  isSubmitting: boolean;
  error: string | null;
}) {
  return (
    <div className="flex flex-col gap-4">
      <div className="rounded-card border border-neutral-200 p-4">
        <h3 className="font-semibold">{basicInfo.title}</h3>
        <p className="mt-1 text-sm text-neutral-600">{basicInfo.description}</p>
        <p className="mt-2 text-sm text-neutral-500">
          {ingredients.length} nguyên liệu · {steps.length} bước thực hiện
        </p>
      </div>

      {error && <p className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      <p className="text-sm text-neutral-500">
        Công thức sẽ được lưu ở trạng thái <strong>Bản nháp (Draft)</strong>. Bạn có thể publish sau khi kiểm tra lại ở trang chỉnh sửa.
      </p>

      <div className="flex justify-between">
        <Button type="button" variant="ghost" onClick={onBack}>
          Quay lại
        </Button>
        <Button type="button" onClick={onSubmit} isLoading={isSubmitting}>
          Lưu công thức
        </Button>
      </div>
    </div>
  );
}
