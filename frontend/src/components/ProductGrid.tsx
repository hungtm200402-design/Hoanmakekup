"use client";

import { useEffect, useState } from "react";
import { ApiProduct, fetchProducts, formatVnd, productBadge } from "@/lib/api";
import { featuredProducts } from "@/lib/featuredProducts";
import { ProductCard } from "./ProductCard";

const assetBase = "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham";

const homeProductImageBySlug: Record<string, string> = {
  "perfect-diary": `${assetBase}/13_combo_trang_diem_tu_nhien.png`,
  "son-black-rouge-air-fit": `${assetBase}/15_combo_trang_diem_toan_dien.png`,
  "innisfree-powder": `${assetBase}/14_combo_duong_sang_da.png`,
  "the-ordinary-serum": `${assetBase}/16_combo_phuc_hoi_cap_am.png`,
  "maybelline-mascara": `${assetBase}/15_combo_trang_diem_toan_dien.png`,
  bioderma: `${assetBase}/13_combo_trang_diem_tu_nhien.png`
};

const shopProductImageBySlug: Record<string, string> = {
  "perfect-diary": `${assetBase}/13_combo_trang_diem_tu_nhien.png`,
  "son-black-rouge-air-fit": `${assetBase}/15_combo_trang_diem_toan_dien.png`,
  "innisfree-powder": `${assetBase}/14_combo_duong_sang_da.png`,
  "the-ordinary-serum": `${assetBase}/16_combo_phuc_hoi_cap_am.png`,
  "maybelline-mascara": `${assetBase}/15_combo_trang_diem_toan_dien.png`,
  bioderma: `${assetBase}/13_combo_trang_diem_tu_nhien.png`
};

export function ProductGrid({ limit, columns = "home" }: { limit?: number; columns?: "home" | "shop" }) {
  const [products, setProducts] = useState<ApiProduct[]>(featuredProducts);

  useEffect(() => {
    fetchProducts()
      .then((items) => setProducts(limit ? items.slice(0, limit) : items))
      .catch(() => {
        setProducts(limit ? featuredProducts.slice(0, limit) : featuredProducts);
      });
  }, [limit]);

  return (
    <div className={columns === "home" ? "mx-auto grid w-full max-w-[1320px] grid-cols-3 justify-center gap-7 max-[900px]:grid-cols-2 max-[620px]:grid-cols-1" : "grid grid-cols-3 gap-7 max-[1024px]:grid-cols-2 max-[640px]:grid-cols-1"}>
      {products.map((product) => (
        <ProductCard
          key={product.id}
          id={product.slug}
          name={product.name}
          price={formatVnd(product.salePrice ?? product.price)}
          oldPrice={product.salePrice ? formatVnd(product.price) : undefined}
          badge={productBadge(product)}
          image={(columns === "home" ? homeProductImageBySlug : shopProductImageBySlug)[product.slug] ?? product.imagePath}
          compact={columns === "home"}
        />
      ))}
    </div>
  );
}
