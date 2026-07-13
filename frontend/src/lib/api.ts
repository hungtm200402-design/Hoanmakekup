import { clearAdminSession, getAdminAuthHeaders, saveAdminSession, type AdminAuthSession } from "./adminAuth";
import { normalizeAppointmentFilters, type AppointmentFilters, type AppointmentStatus } from "./adminAppointments";
import type { OrderStatus } from "./adminOrders";

export type ApiProduct = {
  id: string;
  slug: string;
  name: string;
  price: number;
  salePrice: number | null;
  stock: number;
  imagePath: string;
};

export type AdminProduct = ApiProduct & {
  saleApproved: boolean;
  createdAt: string;
};

export type UpsertProductPayload = {
  slug: string;
  name: string;
  price: number;
  salePrice: number | null;
  saleApproved: boolean;
  stock: number;
  imagePath: string;
};

export type DashboardData = {
  revenueToday: number;
  newOrders: number;
  appointmentsToday: number;
  newCustomers: number;
  appointments: Array<{ startAt: string; customerName: string; service: string; status: string }>;
  bestProducts: Array<{ name: string; stock: number }>;
  topCustomers: Array<{ name: string; count: number }>;
};

export type AdminAppointment = {
  id: string;
  customerName: string;
  phone: string;
  email: string;
  address: string;
  service: string;
  tone: string;
  note: string;
  startAt: string;
  endAt: string;
  status: string;
};

export type AdminOrderItem = {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
};

export type AdminOrder = {
  id: string;
  customerName: string;
  phone: string;
  address: string;
  status: string;
  total: number;
  items: AdminOrderItem[];
  createdAt: string;
};

export type AdminCustomer = {
  phone: string;
  name: string;
  visits: number;
  lastActivityAt: string;
};

export type ProductIdentification = {
  productName: string;
  brand: string;
  productLine: string;
  variant: string;
  shade: string;
  finish: string;
  category: string;
  itemForm: "full-product" | "case" | "refill" | "accessory" | "unknown" | string;
  size: string;
  visibleText: string[];
  confidence: number;
  searchQuery: string;
  needsConfirmation: boolean;
  message: string;
};

export type SaleContentResult = {
  creativeDirection: string;
  headline: string;
  opening: string;
  contentBlocks: Array<{
    type: "paragraph" | "highlights" | "offer" | string;
    title: string;
    text: string;
    items: Array<{
      icon: string;
      benefitTitle: string;
      text: string;
    }>;
  }>;
  callToAction: {
    icon: string;
    text: string;
  };
  contact: {
    shopName: string;
    phone: string;
    address: string;
    website: string;
  };
  hashtags: string[];
  shortCaption: string;
  verifiedDetails: {
    claims: Array<{
      claim: string;
      sourceUrl: string;
      sourceTitle: string;
      matchedProduct: string;
      matchedVariant: string;
      confidence: number;
    }>;
    usage: string[];
    warnings: string[];
    sources: Array<{ website: string; title: string; url: string }>;
  };
  researchSuccessful: boolean;
  warningMessage: string;
};

export type ConfirmedProductPayload = {
  productName: string;
  brand: string;
  productLine: string;
  variant: string;
  shade: string;
  finish: string;
  category: string;
  itemForm: string;
  size: string;
  searchQuery: string;
  userConfirmed: boolean;
  officialProductUrl: string;
  price: string;
  salePrice: string;
  gift: string;
  shopName: string;
  phone: string;
  address: string;
  website: string;
  remainingQuantity: string;
  isRewrite: boolean;
  previousCreativeDirection: string;
};

export type OfficialProductUrlResult = {
  url: string;
  bestUrl: string;
  title: string;
  website: string;
  brand?: string;
  sourceType: string;
  confidence: number;
  matchScore?: number;
  matchedFields: string[];
  sources: Array<{ website: string; title: string; url: string; sourceType?: string; confidence?: number; matchedFields?: string[] | null }>;
  alternativeSources?: Array<{ website: string; title: string; url: string; sourceType?: string; confidence?: number; matchedFields?: string[] | null }>;
  identification?: ProductIdentification | null;
  message: string;
};

type ApiErrorPayload = {
  error?: string;
  code?: string;
  message?: string;
  retryAfterSeconds?: number | null;
  requestId?: string;
};

export type AdminLoginResponse = AdminAuthSession;

export function formatVnd(value?: number | string | null) {
  if (value === null || value === undefined || value === "") {
    return "";
  }

  const numericValue = typeof value === "number"
    ? value
    : Number(String(value).replace(/[^\d]/g, ""));

  if (!Number.isFinite(numericValue) || numericValue <= 0) {
    return "";
  }

  return new Intl.NumberFormat("vi-VN").format(numericValue) + "₫";
}

export function productBadge(product: ApiProduct) {
  return product.slug === "perfect-diary" ? "NEW" : undefined;
}

export async function fetchProducts() {
  const response = await fetch("/api/products", { cache: "no-store" });
  if (!response.ok) {
    throw new Error("Không tải được danh sách sản phẩm.");
  }

  return (await response.json()) as ApiProduct[];
}

export async function fetchProduct(slug: string) {
  const response = await fetch(`/api/products/${slug}`, { cache: "no-store" });
  if (!response.ok) {
    throw new Error("Không tải được sản phẩm.");
  }

  return (await response.json()) as ApiProduct;
}

export async function loginAdmin(username: string, password: string) {
  const response = await fetch("/api/admin/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password })
  });

  if (!response.ok) {
    throw new Error(response.status === 401
      ? "Tên đăng nhập hoặc mật khẩu admin không đúng."
      : `Không đăng nhập được admin. Backend trả mã lỗi ${response.status}.`);
  }

  const data = await response.json() as AdminLoginResponse;
  saveAdminSession(data);
  return data;
}

export function logoutAdmin() {
  clearAdminSession();
}

export async function fetchDashboard() {
  const response = await adminFetch("/api/admin/dashboard", { cache: "no-store" }, "Không tải được dashboard.");

  return (await response.json()) as DashboardData;
}

export async function fetchAdminAppointments(filters: AppointmentFilters = {}, signal?: AbortSignal) {
  const params = new URLSearchParams(normalizeAppointmentFilters(filters));
  const query = params.toString();
  const response = await adminFetch(`/api/admin/appointments${query ? `?${query}` : ""}`, { cache: "no-store", signal }, "Không tải được lịch hẹn.");

  return (await response.json()) as AdminAppointment[];
}

export async function updateAdminAppointmentStatus(id: string, status: AppointmentStatus) {
  const response = await adminFetch(`/api/admin/appointments/${id}/status`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ status })
  }, "Không cập nhật được trạng thái lịch hẹn.");

  return (await response.json()) as AdminAppointment;
}

export async function fetchAdminOrders() {
  const response = await adminFetch("/api/admin/orders", { cache: "no-store" }, "Không tải được đơn hàng.");

  return (await response.json()) as AdminOrder[];
}

export async function updateAdminOrderStatus(id: string, status: OrderStatus) {
  const response = await adminFetch(`/api/admin/orders/${id}/status`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ status })
  }, "Không cập nhật được trạng thái đơn hàng.");
  return (await response.json()) as AdminOrder;
}

export async function fetchAdminProducts() {
  const response = await adminFetch("/api/admin/products", { cache: "no-store" }, "Không tải được sản phẩm admin.");

  return (await response.json()) as AdminProduct[];
}

export async function createAdminProduct(payload: UpsertProductPayload) {
  const response = await adminFetch("/api/admin/products", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  }, "Không tạo được sản phẩm.");
  return (await response.json()) as AdminProduct;
}

export async function updateAdminProduct(id: string, payload: UpsertProductPayload) {
  const response = await adminFetch(`/api/admin/products/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  }, "Không cập nhật được sản phẩm.");
  return (await response.json()) as AdminProduct;
}

export async function deleteAdminProduct(id: string) {
  await adminFetch(`/api/admin/products/${id}`, { method: "DELETE" }, "Không xoá được sản phẩm.");
}

export async function fetchAdminCustomers() {
  const response = await adminFetch("/api/admin/customers", { cache: "no-store" }, "Không tải được khách hàng.");

  return (await response.json()) as AdminCustomer[];
}

export async function identifyProductImage(file: File, signal?: AbortSignal) {
  const formData = new FormData();
  formData.append("image", file);

  const timeout = createTimeoutSignal(signal, 105_000);
  const response = await safeFetch(longRunningApiUrl("/api/admin/ai-content/identify"), {
    method: "POST",
    headers: getAdminAuthHeaders(),
    body: formData,
    signal: timeout.signal
  }, "Không nhận diện được sản phẩm từ ảnh.");
  try {
    const data = await readApiJson<ProductIdentification & ApiErrorPayload>(response);
    if (!response.ok || !data) {
      throw new Error(readApiErrorMessage(data, response, "Không nhận diện được sản phẩm từ ảnh."));
    }

    return data as ProductIdentification;
  } finally {
    timeout.cleanup();
  }
}

export async function findOfficialProductUrl(payload: ConfirmedProductPayload, onStatus?: (message: string) => void, signal?: AbortSignal) {
  const timeout = createTimeoutSignal(signal, 105_000);
  const response = await safeFetch(longRunningApiUrl("/api/admin/ai-content/official-url"), {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAdminAuthHeaders() },
    body: JSON.stringify(payload),
    signal: timeout.signal
  }, "Không tìm được URL sản phẩm đáng tin cậy.");
  try {
    const data = await readApiJson<OfficialProductUrlResult & ApiErrorPayload>(response);
    if (!response.ok || !data) {
      throw new Error(readApiErrorMessage(data, response, "Không tìm được URL sản phẩm đáng tin cậy."));
    }

    if (data.url) {
      onStatus?.(`Đã tìm thấy URL sản phẩm: ${data.website || data.url}`);
    }

    return data as OfficialProductUrlResult;
  } finally {
    timeout.cleanup();
  }
}

export async function findOfficialProductUrlFromImage(payload: ConfirmedProductPayload, file: File, onStatus?: (message: string) => void, signal?: AbortSignal) {
  const formData = new FormData();
  formData.append("image", file);
  formData.append("payload", JSON.stringify(payload));

  const timeout = createTimeoutSignal(signal, 105_000);
  const response = await safeFetch(longRunningApiUrl("/api/admin/ai-content/official-url/image"), {
    method: "POST",
    headers: getAdminAuthHeaders(),
    body: formData,
    signal: timeout.signal
  }, "Không tìm được URL sản phẩm đáng tin cậy từ ảnh.");
  try {
    const data = await readApiJson<OfficialProductUrlResult & ApiErrorPayload>(response);
    console.info("[official-url:image] response", {
      ok: response.ok,
      status: response.status,
      url: data?.url ?? "",
      website: data?.website ?? "",
      title: data?.title ?? "",
      message: data?.message ?? data?.error ?? ""
    });
    if (!response.ok || !data) {
      throw new Error(readApiErrorMessage(data, response, "Không tìm được URL sản phẩm đáng tin cậy từ ảnh."));
    }

    if (data.url) {
      onStatus?.(`Đã tìm thấy URL sản phẩm từ ảnh: ${data.website || data.url}`);
    }

    return data as OfficialProductUrlResult;
  } finally {
    timeout.cleanup();
  }
}

export async function verifyOfficialProductUrl(product: ConfirmedProductPayload, url: string, signal?: AbortSignal) {
  const timeout = createTimeoutSignal(signal, 105_000);
  const response = await safeFetch(longRunningApiUrl("/api/admin/ai-content/verify-url"), {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAdminAuthHeaders() },
    body: JSON.stringify({ product, url }),
    signal: timeout.signal
  }, "Không xác minh được URL sản phẩm.");
  try {
    const data = await readApiJson<OfficialProductUrlResult & ApiErrorPayload>(response);
    if (!response.ok || !data) {
      throw new Error(readApiErrorMessage(data, response, "Không xác minh được URL sản phẩm."));
    }

    return data as OfficialProductUrlResult;
  } finally {
    timeout.cleanup();
  }
}

export async function writeSaleContent(payload: ConfirmedProductPayload, signal?: AbortSignal) {
  const timeout = createTimeoutSignal(signal, 105_000);
  const response = await safeFetch(longRunningApiUrl("/api/admin/ai-content/write"), {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAdminAuthHeaders() },
    body: JSON.stringify(payload),
    signal: timeout.signal
  }, "Không viết được bài sale từ thông tin sản phẩm.");
  try {
    const data = await readApiJson<SaleContentResult & ApiErrorPayload>(response);
    if (response.ok && data) {
      return data as SaleContentResult;
    }

    throw new Error(finalizeApiErrorMessage(readApiErrorMessage(data, response, "Không viết được bài sale từ thông tin sản phẩm.")));
  } finally {
    timeout.cleanup();
  }
}

function createTimeoutSignal(parentSignal: AbortSignal | undefined, timeoutMs: number) {
  const controller = new AbortController();
  const timeoutId = window.setTimeout(() => controller.abort(), timeoutMs);
  const abort = () => controller.abort();
  parentSignal?.addEventListener("abort", abort, { once: true });

  return {
    signal: controller.signal,
    cleanup() {
      window.clearTimeout(timeoutId);
      parentSignal?.removeEventListener("abort", abort);
    }
  };
}

function longRunningApiUrl(path: string) {
  if (typeof window === "undefined") {
    return path;
  }

  const host = window.location.hostname;
  if (host === "localhost" || host === "127.0.0.1") {
    return `http://127.0.0.1:5000${path}`;
  }

  return path;
}

async function readApiJson<T>(response: Response) {
  const text = await response.text();
  if (!text.trim()) {
    return null;
  }

  try {
    return JSON.parse(text) as T;
  } catch {
    return { error: text.slice(0, 240) } as T & { error: string };
  }
}

async function safeFetch(input: RequestInfo | URL, init: RequestInit, fallback: string) {
  try {
    return await fetch(input, init);
  } catch (exception) {
    if (exception instanceof DOMException && exception.name === "AbortError") {
      throw new Error(`${fallback} Quá thời gian tìm kiếm, vui lòng thử lại hoặc dán URL web chính hãng.`);
    }

    throw new Error(`${fallback} Backend chưa kết nối được. Vui lòng chạy lại backend rồi thử lại.`);
  }
}

async function adminFetch(input: RequestInfo | URL, init: RequestInit, fallback: string) {
  const headers = new Headers(init.headers);
  const authHeaders = getAdminAuthHeaders();
  for (const [key, value] of Object.entries(authHeaders)) {
    headers.set(key, value);
  }

  const response = await fetch(input, {
    ...init,
    headers
  });

  if (response.status === 401 || response.status === 403) {
    clearAdminSession();
    throw new Error("Phiên đăng nhập admin đã hết hạn hoặc không có quyền. Vui lòng đăng nhập lại.");
  }

  if (!response.ok) {
    const data = await readApiJson<ApiErrorPayload>(response.clone());
    throw new Error(normalizeApiErrorMessage(data?.error ?? data?.message ?? `${fallback} HTTP ${response.status}.`));
  }

  return response;
}

function readApiErrorMessage(data: ApiErrorPayload | null, response: Response, fallback: string) {
  if (data?.code === "GEMINI_QUOTA_EXCEEDED") {
    return data.message || "Gemini API đã hết hạn mức. Vui lòng kiểm tra Usage, Rate limits và Billing của project.";
  }

  if (data?.code === "REQUEST_TIMEOUT") {
    return data.message || "Yêu cầu AI mất quá nhiều thời gian. Vui lòng thử lại.";
  }

  if (data?.code === "USER_CANCELLED") {
    return data.message || "Yêu cầu đã bị hủy.";
  }

  if (data?.code === "TRUSTED_URL_NOT_FOUND") {
    return data.message || `${fallback} Chưa có nguồn tin cậy khớp sản phẩm.`;
  }

  return normalizeApiErrorMessage(data?.message ?? data?.error ?? buildHttpError(response, fallback));
}

function buildHttpError(response: Response, fallback: string) {
  if (response.status === 429) {
    return "Gemini API đã hết hạn mức. Vui lòng kiểm tra Usage, Rate limits và Billing của project.";
  }

  if (response.status === 500) {
    return `${fallback} Backend đang lỗi nội bộ. Vui lòng xem log backend-run để biết nguyên nhân chi tiết.`;
  }

  if (response.status === 422) {
    return "Bản nháp chưa đạt chất lượng bán hàng. Hệ thống đã chặn câu quá chung chung, vui lòng bấm Viết lại.";
  }

  if (response.status === 503) {
    return `${fallback} Hoàn Doãn Beauty & Academy đang bận hoặc quá tải. Vui lòng chờ vài giây rồi thử lại.`;
  }

  if (response.status === 504) {
    return `${fallback} Yêu cầu AI mất quá nhiều thời gian. Vui lòng thử lại với ảnh rõ hơn hoặc nhập thêm tên sản phẩm.`;
  }

  return `${fallback} HTTP ${response.status}${response.statusText ? `: ${response.statusText}` : ""}.`;
}

function normalizeApiErrorMessage(message: string) {
  if (message.trim() === "Internal Server Error") {
    return "Backend đang lỗi nội bộ. Vui lòng xem log backend-run để biết nguyên nhân chi tiết.";
  }

  const oldSearchQuotaMessage = [
    ["Nguồn", "tìm kiếm đang", "vượt hạn mức."].join(" "),
    ["Vui lòng thử lại sau hoặc nhập", "đường dẫn sản phẩm chính thức"].join(" "),
    ["để Hoàn Doãn Beauty & Academy", "dùng", "làm nguồn", "thay thế."].join(" ")
  ].join(" ");
  if (message.includes(oldSearchQuotaMessage)) {
    return message.replace(
      oldSearchQuotaMessage,
      "Hoàn Doãn Beauty & Academy đang bị giới hạn ở backend cũ. Vui lòng restart backend/frontend để dùng bản mới có tự tìm nguồn uy tín và fallback khi quá tải."
    );
  }

  return message;
}

function finalizeApiErrorMessage(message: string) {
  if (isGeminiRateLimitMessage(message)) {
    return "Hoàn Doãn Beauty & Academy vẫn đang bị giới hạn lượt xử lý sau khi hệ thống đã tự chờ và thử lại. Vui lòng đợi hệ thống hồi thêm một lúc rồi thử lại.";
  }

  return message;
}

function isGeminiRateLimitMessage(message: string) {
  const normalized = message.toLowerCase();
  return normalized.includes("rpm/tpm") ||
    normalized.includes("rate limit") ||
    normalized.includes("too many requests") ||
    normalized.includes("giới hạn gọi nhanh") ||
    normalized.includes("giới hạn lượt gọi") ||
    normalized.includes("bị giới hạn");
}
