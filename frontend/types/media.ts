/** GET /files/mine (Tuần 10) — một ảnh trong "Thư viện ảnh của tôi". Khớp MyImageDto ở backend. */
export interface MyImage {
  id: string;
  url: string;
  thumbnailUrl: string | null;
  fileName: string;
  contentType: string;
  size: number;
  createdAt: string;
}

export interface MyImagePage {
  items: MyImage[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
}
