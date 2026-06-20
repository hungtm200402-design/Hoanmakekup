"use client";

import { useEffect, useState } from "react";
import { ApiProduct, fetchProducts, formatVnd, productBadge } from "@/lib/api";
import { ProductCard } from "./ProductCard";

const assetBase = "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham";

const homeProductImageBySlug: Record<string, string> = {
  "perfect-diary": `${assetBase}/01_home_serum_cang_bong.png`,
  "son-black-rouge-air-fit": `${assetBase}/05_home_son_li_hoan_doan.png`,
  "innisfree-powder": `${assetBase}/02_home_kem_duong_am.png`,
  "the-ordinary-serum": `${assetBase}/03_home_sua_rua_mat_hoa_hong.png`,
  "maybelline-mascara": `${assetBase}/06_home_bo_co_trang_diem_cao_cap.png`,
  bioderma: `${assetBase}/04_home_kem_chong_nang_tone_up.png`
};

const shopProductImageBySlug: Record<string, string> = {
  "perfect-diary": `${assetBase}/07_shop_kem_nen_fit_me_matte.png`,
  "son-black-rouge-air-fit": `${assetBase}/08_shop_son_mac_matte_lipstick.png`,
  "innisfree-powder": `${assetBase}/09_shop_phan_phu_infallible.png`,
  "the-ordinary-serum": `${assetBase}/10_shop_serum_niacinamide.png`,
  "maybelline-mascara": `${assetBase}/11_shop_mascara_dai_cong_mi.png`,
  bioderma: `${assetBase}/12_shop_nuoc_tay_trang_micellar.png`
};

export function ProductGrid({ limit, columns = "home" }: { limit?: number; columns?: "home" | "shop" }) {
  const [products, setProducts] = useState<ApiProduct[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    fetchProducts()
      .then((items) => setProducts(limit ? items.slice(0, limit) : items))
      .catch((exception: Error) => setError(exception.message));
  }, [limit]);

  if (error) {
    return <p className="mt-8 rounded border border-brand-line bg-brand-pale p-4 text-center text-[14px] text-brand-red">{error}</p>;
  }

  if (products.length === 0) {
    return <p className="mt-8 text-center text-[14px] text-brand-muted">Đang tải sản phẩm...</p>;
  }

  return (
    <div className={columns === "home" ? "grid grid-cols-6 gap-5 max-[1180px]:grid-cols-3 max-[620px]:grid-cols-2" : "grid grid-cols-3 gap-7 max-[1024px]:grid-cols-2 max-[640px]:grid-cols-1"}>
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
