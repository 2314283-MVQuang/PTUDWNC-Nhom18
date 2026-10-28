"use client";

import { useState } from "react";
import { Check, Pencil, Plus, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { apiErrorMessage } from "@/lib/api-client";
import { formatDecimal } from "@/lib/utils";
import {
  useAddIngredient,
  useDeleteIngredient,
  useUpdateIngredient,
} from "@/hooks/useRecipeParts";
import type { RecipeIngredient } from "@/types/recipe";

/**
 * FR-RCP-009 — thêm / sửa / xoá từng nguyên liệu.
 *
 * Mỗi dòng có hai trạng thái: xem và sửa. Bấm bút chì để chuyển sang sửa, lưu xong quay lại xem.
 * Không dùng react-hook-form ở đây vì số dòng thay đổi liên tục và mỗi dòng chỉ có 4 ô đơn giản;
 * useState cục bộ cho từng dòng gọn hơn là một form động.
 */
export function IngredientsEditor({
  recipeId,
  ingredients,
}: {
  recipeId: string;
  ingredients: RecipeIngredient[];
}) {
  const addIngredient = useAddIngredient(recipeId);
  const [error, setError] = useState<string | null>(null);
  const [draft, setDraft] = useState({ name: "", quantity: "", unit: "", notes: "" });

  const sorted = ingredients.slice().sort((a, b) => a.orderIndex - b.orderIndex);

  async function handleAdd() {
    if (!draft.name.trim()) {
      setError("Nhập tên nguyên liệu trước đã.");
      return;
    }
    setError(null);
    try {
      await addIngredient.mutateAsync({
        name: draft.name.trim(),
        // Backend có CHECK Quantity > 0, nên ô trống phải gửi null chứ không phải 0.
        quantity: draft.quantity ? Number(draft.quantity) : null,
        unit: draft.unit.trim() || null,
        notes: draft.notes.trim() || null,
      });
      setDraft({ name: "", quantity: "", unit: "", notes: "" });
    } catch (err) {
      setError(apiErrorMessage(err, "Không thêm được nguyên liệu."));
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {sorted.length === 0 && (
        <p className="text-sm text-neutral-500">Chưa có nguyên liệu nào.</p>
      )}

      <ul className="flex flex-col divide-y divide-neutral-100">
        {sorted.map((ingredient) => (
          <IngredientRow key={ingredient.id} recipeId={recipeId} ingredient={ingredient} />
        ))}
      </ul>

      <div className="rounded-card border border-dashed border-neutral-300 p-3">
        <div className="grid gap-2 sm:grid-cols-[2fr_1fr_1fr_2fr]">
          <Input
            placeholder="Tên nguyên liệu"
            value={draft.name}
            onChange={(e) => setDraft({ ...draft, name: e.target.value })}
          />
          <Input
            type="number"
            step="any"
            placeholder="Số lượng"
            value={draft.quantity}
            onChange={(e) => setDraft({ ...draft, quantity: e.target.value })}
          />
          <Input
            placeholder="Đơn vị"
            value={draft.unit}
            onChange={(e) => setDraft({ ...draft, unit: e.target.value })}
          />
          <Input
            placeholder="Ghi chú (tuỳ chọn)"
            value={draft.notes}
            onChange={(e) => setDraft({ ...draft, notes: e.target.value })}
          />
        </div>

        {error && <p className="mt-2 text-sm text-spice-600">{error}</p>}

        <div className="mt-2 flex justify-end">
          <Button size="sm" variant="outline" isLoading={addIngredient.isPending} onClick={handleAdd}>
            <Plus className="h-4 w-4" />
            Thêm nguyên liệu
          </Button>
        </div>
      </div>
    </div>
  );
}

function IngredientRow({
  recipeId,
  ingredient,
}: {
  recipeId: string;
  ingredient: RecipeIngredient;
}) {
  const updateIngredient = useUpdateIngredient(recipeId);
  const deleteIngredient = useDeleteIngredient(recipeId);

  const [isEditing, setIsEditing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [draft, setDraft] = useState({
    name: ingredient.name,
    quantity: ingredient.quantity?.toString() ?? "",
    unit: ingredient.unit ?? "",
    notes: ingredient.notes ?? "",
  });

  async function handleSave() {
    setError(null);
    try {
      await updateIngredient.mutateAsync({
        ingredientId: ingredient.id,
        input: {
          name: draft.name.trim(),
          quantity: draft.quantity ? Number(draft.quantity) : null,
          unit: draft.unit.trim() || null,
          notes: draft.notes.trim() || null,
        },
      });
      setIsEditing(false);
    } catch (err) {
      setError(apiErrorMessage(err, "Không lưu được nguyên liệu."));
    }
  }

  async function handleDelete() {
    if (!confirm(`Xoá nguyên liệu "${ingredient.name}"?`)) return;
    setError(null);
    try {
      await deleteIngredient.mutateAsync(ingredient.id);
    } catch (err) {
      setError(apiErrorMessage(err, "Không xoá được nguyên liệu."));
    }
  }

  if (isEditing) {
    return (
      <li className="py-2">
        <div className="grid gap-2 sm:grid-cols-[2fr_1fr_1fr_2fr]">
          <Input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
          <Input
            type="number"
            step="any"
            value={draft.quantity}
            onChange={(e) => setDraft({ ...draft, quantity: e.target.value })}
          />
          <Input value={draft.unit} onChange={(e) => setDraft({ ...draft, unit: e.target.value })} />
          <Input value={draft.notes} onChange={(e) => setDraft({ ...draft, notes: e.target.value })} />
        </div>

        {error && <p className="mt-1 text-sm text-spice-600">{error}</p>}

        <div className="mt-2 flex justify-end gap-2">
          <Button size="sm" variant="ghost" onClick={() => setIsEditing(false)}>
            <X className="h-4 w-4" />
            Huỷ
          </Button>
          <Button size="sm" isLoading={updateIngredient.isPending} onClick={handleSave}>
            <Check className="h-4 w-4" />
            Lưu
          </Button>
        </div>
      </li>
    );
  }

  return (
    <li className="flex items-center justify-between gap-3 py-2 text-sm">
      <div className="min-w-0">
        <span className="text-neutral-800">{ingredient.name}</span>
        {ingredient.notes && (
          <span className="ml-2 text-xs text-neutral-500">({ingredient.notes})</span>
        )}
      </div>

      <div className="flex shrink-0 items-center gap-1">
        <span className="whitespace-nowrap text-neutral-500">
          {formatDecimal(ingredient.quantity)} {ingredient.unit ?? ""}
        </span>
        <button
          type="button"
          aria-label={`Sửa ${ingredient.name}`}
          className="rounded-lg p-1.5 text-neutral-500 hover:bg-brand-50 hover:text-brand-700"
          onClick={() => setIsEditing(true)}
        >
          <Pencil className="h-4 w-4" />
        </button>
        <button
          type="button"
          aria-label={`Xoá ${ingredient.name}`}
          disabled={deleteIngredient.isPending}
          className="rounded-lg p-1.5 text-neutral-500 hover:bg-spice-50 hover:text-spice-600 disabled:opacity-50"
          onClick={handleDelete}
        >
          <Trash2 className="h-4 w-4" />
        </button>
      </div>

      {error && <p className="w-full text-sm text-spice-600">{error}</p>}
    </li>
  );
}
