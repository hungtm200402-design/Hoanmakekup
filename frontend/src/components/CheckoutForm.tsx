"use client";

import { FormEvent, useEffect, useState } from "react";
import { ApiProduct, fetchProduct, formatVnd } from "@/lib/api";

export function CheckoutForm() {
  const [product, setProduct] = useState<ApiProduct | null>(null);
  const [message, setMessage] = useState("");

  useEffect(() => {
    fetchProduct("son-black-rouge-air-fit")
      .then(setProduct)
      .catch((exception: Error) => setMessage(exception.message));
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!product) {
      return;
    }

    const formData = new FormData(event.currentTarget);
    setMessage("Đang gửi đơn hàng...");

    const response = await fetch("/api/orders", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        customerName: formData.get("customerName"),
        phone: formData.get("phone"),
        address: formData.get("address"),
        items: [{ productId: product.id, quantity: 1 }]
      })
    });

    setMessage(response.ok ? "Đơn hàng đã được tạo. Backend đã kiểm tra lại giá và tồn kho." : "Không tạo được đơn hàng. Vui lòng kiểm tra backend/database.");
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
        <p className="mt-5 flex justify-between gap-4"><span>{product?.name ?? "Đang tải sản phẩm..."}</span><strong>{product ? formatVnd(product.salePrice ?? product.price) : "..."}</strong></p>
        <p className="mt-5 flex justify-between border-t border-brand-line pt-5 text-[18px]"><span>Tổng cộng</span><strong className="text-brand-red">{product ? formatVnd(product.salePrice ?? product.price) : "..."}</strong></p>
        {message ? <p className="mt-5 rounded bg-brand-pale p-3 text-[13px] font-semibold text-brand-red">{message}</p> : null}
        <button className="btn-red mt-7 w-full" type="submit" disabled={!product}>Đặt hàng</button>
      </aside>
    </form>
  );
}
