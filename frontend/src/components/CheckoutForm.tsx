"use client";

import { FormEvent, startTransition, useEffect, useState } from "react";
import { formatVnd } from "@/lib/api";
import {
  clearCartItems,
  getCartSummary,
  getCartUnitPrice,
  loadCartItems,
  notifyCartChanged,
  saveCartItems,
  type CartItem
} from "@/lib/cart";

export function CheckoutForm() {
  const [cartItems, setCartItems] = useState<CartItem[]>([]);
  const [message, setMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const summary = getCartSummary(cartItems);

  useEffect(() => {
    startTransition(() => setCartItems(loadCartItems()));
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (cartItems.length === 0) {
      setMessage("Giỏ hàng đang trống. Vui lòng thêm sản phẩm trước khi đặt hàng.");
      return;
    }

    const formData = new FormData(event.currentTarget);
    const customerName = String(formData.get("customerName") ?? "").trim();
    const phone = String(formData.get("phone") ?? "").trim();
    const address = String(formData.get("address") ?? "").trim();
    if (!customerName || !phone || !address) {
      setMessage("Vui lòng nhập đầy đủ tên, số điện thoại và địa chỉ.");
      return;
    }

    setSubmitting(true);
    setMessage("Đang gửi đơn hàng...");

    try {
      const response = await fetch("/api/orders", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          customerName,
          phone,
          address,
          items: cartItems.map((item) => ({ productId: item.id, quantity: item.quantity }))
        })
      });

      const data = await readApiJson<{ id?: string; total?: number; error?: string }>(response);
      if (!response.ok) {
        setMessage(readOrderError(response.status, data?.error));
        return;
      }

      const orderCode = data?.id ? `#${data.id.slice(0, 8).toUpperCase()}` : "mới";
      setCartItems(clearCartItems());
      saveCartItems(clearCartItems());
      notifyCartChanged();
      setMessage(`Đơn hàng ${orderCode} đã được tạo thành công. Backend đã kiểm tra lại giá và tồn kho. Tổng tiền: ${formatVnd(data?.total ?? summary.totalPrice)}.`);
      event.currentTarget.reset();
    } catch {
      setMessage("Không kết nối được backend. Vui lòng chạy backend ở cổng 5000 rồi thử lại.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={submit} className="mt-9 grid grid-cols-[1fr_380px] gap-8 max-[900px]:grid-cols-1">
      <section className="grid gap-4 rounded border border-brand-line p-6">
        <h2 className="text-[20px] font-bold">Thông tin nhận hàng</h2>
        <input name="customerName" required className="h-12 rounded border border-brand-line px-4 outline-none focus:border-brand-red" placeholder="Nguyễn Thị Hoàn" />
        <input name="phone" required className="h-12 rounded border border-brand-line px-4 outline-none focus:border-brand-red" placeholder="0123 456 789" />
        <input name="address" required className="h-12 rounded border border-brand-line px-4 outline-none focus:border-brand-red" placeholder="123 Đường ABC, Quận 1, TP.HCM" />
        <select className="h-12 rounded border border-brand-line px-4 outline-none focus:border-brand-red"><option>Thanh toán khi nhận hàng</option><option>Chuyển khoản ngân hàng</option></select>
        <p className="text-[12px] text-brand-muted">Không lưu thông tin thẻ, OTP hoặc CVV. Backend sẽ tự kiểm tra giá và tồn kho trước khi tạo đơn.</p>
      </section>
      <aside className="rounded border border-brand-line p-6">
        <h2 className="text-[20px] font-bold">Đơn hàng</h2>
        <div className="mt-5 grid gap-3">
          {cartItems.length === 0 ? (
            <p className="rounded border border-brand-line bg-brand-pale p-3 text-[13px] font-semibold text-brand-red">Giỏ hàng đang trống.</p>
          ) : cartItems.map((item) => (
            <p key={item.id} className="grid grid-cols-[1fr_auto] gap-4 text-[14px]">
              <span>{item.name} <small className="text-brand-muted">x{item.quantity}</small></span>
              <strong>{formatVnd(getCartUnitPrice(item) * item.quantity)}</strong>
            </p>
          ))}
        </div>
        <p className="mt-5 flex justify-between border-t border-brand-line pt-5 text-[14px]"><span>Tổng số lượng</span><strong>{summary.totalQuantity}</strong></p>
        <p className="mt-3 flex justify-between text-[18px]"><span>Tổng cộng</span><strong className="text-brand-red">{formatVnd(summary.totalPrice)}</strong></p>
        <p className="mt-3 text-[12px] text-brand-muted">Tổng tiền cuối cùng được backend tính lại theo giá và tồn kho hiện tại.</p>
        {message ? <p className="mt-5 rounded bg-brand-pale p-3 text-[13px] font-semibold text-brand-red">{message}</p> : null}
        <button className="btn-red mt-7 w-full" type="submit" disabled={submitting || cartItems.length === 0}>{submitting ? "Đang đặt hàng..." : "Đặt hàng"}</button>
      </aside>
    </form>
  );
}

async function readApiJson<T>(response: Response) {
  const text = await response.text();
  if (!text.trim()) {
    return null;
  }

  try {
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
}

function readOrderError(status: number, error?: string) {
  if (error) {
    if (error.includes("Tồn kho không đủ")) {
      return "Một hoặc nhiều sản phẩm đã hết hàng hoặc không đủ tồn kho. Vui lòng quay lại giỏ hàng kiểm tra số lượng.";
    }

    return error;
  }

  if (status >= 500) {
    return "Backend đang lỗi nội bộ. Vui lòng thử lại sau hoặc kiểm tra log backend.";
  }

  return `Không tạo được đơn hàng. Backend trả mã lỗi ${status}.`;
}
