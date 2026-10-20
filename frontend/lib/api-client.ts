import type { ApiEnvelope, PagedResult, ProblemDetails } from "@/types/common";

/**
 * Lớp fetch dùng chung cho TOÀN BỘ app — không component/hook nào được gọi `fetch()` trực tiếp
 * tới backend. Lý do gom về 1 chỗ:
 *   1. Tự động gắn base URL + Bearer token.
 *   2. Tự động parse lỗi RFC 7807 (mục 10) thành `ApiError` để component chỉ cần try/catch.
 *   3. Dễ thêm log/retry/refresh-token sau này mà không phải sửa từng nơi gọi API.
 */

const publicApiUrl = process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5000/api/v1";
const API_BASE_URL = typeof window === "undefined"
  ? process.env.API_INTERNAL_BASE_URL || publicApiUrl : publicApiUrl;

/** Lỗi ném ra khi API trả về response không thành công (4xx/5xx), theo format RFC 7807. */
export class ApiError extends Error {
  readonly status: number;
  readonly type: string;
  readonly errors?: Record<string, string[]>;

  constructor(problem: ProblemDetails) {
    super(problem.detail || problem.title);
    this.name = "ApiError";
    this.status = problem.status;
    this.type = problem.type;
    this.errors = problem.errors;
  }
}

export class ApiNetworkError extends Error {
  constructor() {
    super("Không thể kết nối tới máy chủ. Vui lòng thử lại sau.");
    this.name = "ApiNetworkError";
  }
}

async function fetchApi(url: string, init: RequestInit & { next?: NextFetchRequestConfig }): Promise<Response> {
  try {
    return await fetch(url, init);
  } catch (error) {
    if (error instanceof Error && error.name === "AbortError") throw error;
    throw new ApiNetworkError();
  }
}

export interface ApiFetchOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  /** JWT access token — truyền vào khi gọi endpoint yêu cầu Bearer (mục 8, header Authorization). */
  token?: string;
  /** Query string params, undefined/null sẽ tự bị bỏ qua. */
  params?: Record<string, string | number | boolean | undefined | null>;
  /** RowVersion (base64) cho concurrency check — gắn vào header If-Match (FR-RCP-004). */
  ifMatch?: string;
  /** Next.js cache hint — dùng cho ISR ở Server Component (mục 9: revalidate=3600, 300, 600...). */
  next?: NextFetchRequestConfig;
  cache?: RequestCache;
}

function buildUrl(path: string, params?: ApiFetchOptions["params"]): string {
  const url = new URL(`${API_BASE_URL}${path}`);
  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== "") {
        url.searchParams.set(key, String(value));
      }
    }
  }
  return url.toString();
}

/** Gọi API, tự unwrap `{ data }` và ném ApiError nếu thất bại. Dùng cho response KHÔNG phân trang. */
export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  const { method = "GET", body, token, params, ifMatch, next, cache } = options;

  const headers: HeadersInit = { "Content-Type": "application/json" };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  if (ifMatch) headers["If-Match"] = ifMatch;

  const res = await fetchApi(buildUrl(path, params), {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
    next,
    cache,
  });

  if (!res.ok) {
    const problem = await safeParseProblem(res);
    throw new ApiError(problem);
  }

  // 204 No Content (vd. DELETE, logout) không có body để parse.
  if (res.status === 204) return undefined as T;

  const json = (await res.json()) as ApiEnvelope<T>;
  return json.data;
}

/** Giống `apiFetch` nhưng dùng cho các endpoint danh sách có phân trang (trả kèm `meta`). */
export async function apiFetchPaged<T>(
  path: string,
  options: ApiFetchOptions = {},
): Promise<PagedResult<T>> {
  const { method = "GET", token, params, next, cache } = options;
  const headers: HeadersInit = {};
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const res = await fetchApi(buildUrl(path, params), { method, headers, next, cache });

  if (!res.ok) {
    const problem = await safeParseProblem(res);
    throw new ApiError(problem);
  }

  const json = (await res.json()) as ApiEnvelope<T[]>;
  return {
    items: json.data,
    meta: json.meta ?? { page: 1, pageSize: json.data.length, total: json.data.length, totalPages: 1 },
  };
}

/** Upload file multipart (FR-FILE-001, FR-RCP-008) — tách riêng vì không gửi Content-Type JSON. */
export async function apiUpload<T>(
  path: string,
  formData: FormData,
  token?: string,
): Promise<T> {
  const headers: HeadersInit = {};
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const res = await fetchApi(buildUrl(path), { method: "POST", headers, body: formData });

  if (!res.ok) {
    const problem = await safeParseProblem(res);
    throw new ApiError(problem);
  }
  const json = (await res.json()) as ApiEnvelope<T>;
  return json.data;
}

/** Parse body lỗi an toàn — phòng trường hợp server trả lỗi không đúng format RFC 7807 (vd. 502 từ Nginx). */
async function safeParseProblem(res: Response): Promise<ProblemDetails> {
  const fallback: ProblemDetails = {
    type: "UNKNOWN_ERROR",
    title: res.status >= 500 ? "Máy chủ đang gặp sự cố. Vui lòng thử lại sau." : "Không thể thực hiện yêu cầu.",
    status: res.status,
  };
  try {
    const body: unknown = await res.json();
    if (!body || typeof body !== "object" || Array.isArray(body)) return fallback;
    const problem = body as Record<string, unknown>;
    const errors: Record<string, string[]> = {};
    if (problem.errors && typeof problem.errors === "object" && !Array.isArray(problem.errors)) {
      for (const [key, values] of Object.entries(problem.errors)) {
        if (Array.isArray(values) && values.every(value => typeof value === "string")) errors[key] = values;
      }
    }
    return {
      ...fallback,
      type: typeof problem.type === "string" ? problem.type : fallback.type,
      title: typeof problem.title === "string" && problem.title ? problem.title : fallback.title,
      detail: typeof problem.detail === "string" ? problem.detail : undefined,
      errors,
    };
  } catch {
    return fallback;
  }
}

/**
 * Rút thông báo dễ đọc nhất ra khỏi lỗi API.
 *
 * Backend trả lỗi validate theo RFC 7807 với `errors` là map field -> mảng thông báo (xem
 * GlobalExceptionMiddleware). Những thông báo đó cụ thể hơn hẳn `detail` chung chung
 * ("Một hoặc nhiều trường dữ liệu không hợp lệ."), nên ưu tiên hiển thị chúng — ví dụ khi
 * upload file giả mạo, người dùng cần thấy "File không hợp lệ: nội dung không phải ảnh…"
 * chứ không phải câu chung.
 */
export function apiErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiNetworkError) return error.message;
  if (error instanceof ApiError) {
    const fieldMessages = Object.values(error.errors ?? {}).flat();
    if (fieldMessages.length > 0) return fieldMessages.join(" ");
    return error.message || fallback;
  }
  return fallback;
}
