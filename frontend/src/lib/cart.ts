import type { ApiProduct } from "./api";

export const cartStorageKey = "hoanmakeup-cart-v1";

export type CartProduct = Pick<ApiProduct, "id" | "slug" | "name" | "price" | "salePrice" | "stock" | "imagePath">;

export type CartItem = CartProduct & {
  quantity: number;
};

export type CartChangeDetail = {
  productName?: string;
};

export function getCartUnitPrice(item: Pick<CartItem, "price" | "salePrice">) {
  return item.salePrice ?? item.price;
}

export function addCartItem(items: CartItem[], product: CartProduct, quantity = 1) {
  const nextQuantity = normalizeQuantity(quantity);
  const existing = items.find((item) => item.id === product.id);
  if (!existing) {
    return [...items, { ...product, quantity: clampQuantity(nextQuantity, product.stock) }];
  }

  return items.map((item) =>
    item.id === product.id
      ? { ...product, quantity: clampQuantity(item.quantity + nextQuantity, product.stock) }
      : item
  );
}

export function setCartItemQuantity(items: CartItem[], productId: string, quantity: number) {
  if (quantity <= 0) {
    return removeCartItem(items, productId);
  }

  return items.map((item) =>
    item.id === productId
      ? { ...item, quantity: clampQuantity(quantity, item.stock) }
      : item
  );
}

export function increaseCartItem(items: CartItem[], productId: string) {
  return items.map((item) =>
    item.id === productId
      ? { ...item, quantity: clampQuantity(item.quantity + 1, item.stock) }
      : item
  );
}

export function decreaseCartItem(items: CartItem[], productId: string) {
  return items.map((item) =>
    item.id === productId
      ? { ...item, quantity: Math.max(1, item.quantity - 1) }
      : item
  );
}

export function removeCartItem(items: CartItem[], productId: string) {
  return items.filter((item) => item.id !== productId);
}

export function clearCartItems() {
  return [];
}

export function getCartSummary(items: CartItem[]) {
  return items.reduce(
    (summary, item) => ({
      totalQuantity: summary.totalQuantity + item.quantity,
      totalPrice: summary.totalPrice + getCartUnitPrice(item) * item.quantity
    }),
    { totalQuantity: 0, totalPrice: 0 }
  );
}

export function serializeCartItems(items: CartItem[]) {
  return JSON.stringify(items);
}

export function parseCartItems(value: string | null) {
  if (!value) {
    return [];
  }

  try {
    const parsed = JSON.parse(value) as Partial<CartItem>[];
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .filter(isStoredCartItem)
      .map((item) => ({ ...item, quantity: clampQuantity(item.quantity, item.stock) }));
  } catch {
    return [];
  }
}

export function loadCartItems(storage: Pick<Storage, "getItem"> = window.localStorage) {
  return parseCartItems(storage.getItem(cartStorageKey));
}

export function saveCartItems(items: CartItem[], storage: Pick<Storage, "setItem"> = window.localStorage) {
  storage.setItem(cartStorageKey, serializeCartItems(items));
}

export function reconcileCartItems(items: CartItem[], products: CartProduct[]) {
  const productById = new Map(products.map((product) => [product.id, product]));

  return items.flatMap((item) => {
    const product = productById.get(item.id);
    if (!product) {
      return [];
    }

    return [{ ...product, quantity: clampQuantity(item.quantity, product.stock) }];
  });
}

export function notifyCartChanged(detail: CartChangeDetail = {}) {
  window.dispatchEvent(new CustomEvent<CartChangeDetail>("hoanmakeup-cart-changed", { detail }));
}

function normalizeQuantity(quantity: number) {
  return Number.isFinite(quantity) ? Math.max(1, Math.floor(quantity)) : 1;
}

function clampQuantity(quantity: number, stock: number) {
  const normalized = normalizeQuantity(quantity);
  return stock > 0 ? Math.min(normalized, stock) : normalized;
}

function isStoredCartItem(item: Partial<CartItem>): item is CartItem {
  return Boolean(
    item &&
    typeof item.id === "string" &&
    typeof item.slug === "string" &&
    typeof item.name === "string" &&
    typeof item.price === "number" &&
    (typeof item.salePrice === "number" || item.salePrice === null) &&
    typeof item.stock === "number" &&
    typeof item.imagePath === "string" &&
    typeof item.quantity === "number"
  );
}
