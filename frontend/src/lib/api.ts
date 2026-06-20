export type ApiProduct = {
  id: string;
  slug: string;
  name: string;
  price: number;
  salePrice: number | null;
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

export function formatVnd(value: number) {
  return new Intl.NumberFormat("vi-VN").format(value) + "đ";
}

export function productBadge(product: ApiProduct) {
  if (!product.salePrice || product.salePrice >= product.price) {
    return product.slug === "perfect-diary" ? "NEW" : undefined;
  }

  return `-${Math.round(((product.price - product.salePrice) / product.price) * 100)}%`;
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

export async function fetchDashboard() {
  const response = await fetch("/api/admin/dashboard", { cache: "no-store" });
  if (!response.ok) {
    throw new Error("Không tải được dashboard.");
  }

  return (await response.json()) as DashboardData;
}
