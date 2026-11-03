/** FR-CMT (Tuần 9) — khớp CommentDto của GET/POST /recipes/{id}/comments, PUT /comments/{id}. */
export interface RecipeComment {
  id: string;
  recipeId: string;
  content: string;
  author: { id: string; displayName: string; avatarUrl: string | null };
  createdAt: string;
  updatedAt: string | null;
  isEdited: boolean;
  /** Người đang xem là người viết → hiện nút Sửa. */
  canEdit: boolean;
  /** Người viết, tác giả công thức hoặc Admin → hiện nút Xoá. */
  canDelete: boolean;
}

