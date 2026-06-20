"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { ApiProduct, fetchProduct, formatVnd } from "@/lib/api";
import { productDetails } from "@/lib/productDetails";

export default function ProductDetailPage() {
  const params = useParams<{ slug: string }>();
  const slug = params.slug;
  const [qty, setQty] = useState(1);
  const [product, setProduct] = useState<ApiProduct | null>(null);
  const [error, setError] = useState("");
  const detail = productDetails[slug];

  const percent = useMemo(() => {
    if (!product?.salePrice || product.salePrice >= product.price) {
      return 0;
    }

    return Math.round(((product.price - product.salePrice) / product.price) * 100);
  }, [product]);

  useEffect(() => {
    fetchProduct(slug)
      .then(setProduct)
      .catch((exception: Error) => setError(exception.message));
  }, [slug]);

  const image = detail?.detailImage ?? product?.imagePath;

  return (
    <>
      <Header />
      <main className="bg-white">
        <section className="container-beauty py-6 text-[12px] text-brand-muted">Trang chủ / Shop mỹ phẩm / {detail?.category ?? "Sản phẩm"}</section>
        {error ? <p className="container-beauty rounded border border-brand-line bg-brand-pale p-4 text-brand-red">{error}</p> : null}

        <section className="container-beauty grid grid-cols-[1.05fr_0.95fr] gap-10 pb-10 max-[900px]:grid-cols-1">
          <div className="overflow-hidden rounded bg-brand-soft">
            {image ? <img className="h-[520px] w-full object-contain p-8 max-[520px]:h-[340px]" src={image} alt={product?.name ?? "Sản phẩm"} /> : <div className="h-[520px]" />}
          </div>

          <div className="pt-2">
            <h1 className="text-[28px] font-bold">{product?.name ?? "Đang tải sản phẩm..."}</h1>
            <p className="mt-4 text-[14px]"><span className="text-[#ffb800]">★★★★★</span> <span className="text-brand-muted">(128 đánh giá)</span></p>
            <div className="mt-6 flex flex-wrap items-center gap-5">
              <strong className="text-[26px] text-brand-red">{product ? formatVnd(product.salePrice ?? product.price) : "..."}</strong>
              {product?.salePrice ? <span className="text-[14px] text-brand-muted line-through">{formatVnd(product.price)}</span> : null}
              {percent > 0 ? <span className="rounded bg-brand-soft px-3 py-2 text-[12px] font-bold text-brand-red">-{percent}%</span> : null}
            </div>

            <ul className="mt-6 grid gap-3 text-[14px] text-brand-muted">
              {(detail?.bullets ?? []).map((item) => <li key={item}>+ {item}</li>)}
            </ul>

            {detail ? (
              <div className="mt-6">
                <p className="text-[14px] font-bold">{detail.optionsTitle}</p>
                <div className="mt-3 flex flex-wrap gap-3">
                  {detail.options.map((item) => <span key={item} className="rounded border border-brand-line bg-white px-3 py-2 text-[12px] font-semibold">{item}</span>)}
                </div>
              </div>
            ) : null}

            <div className="mt-7">
              <p className="mb-3 text-[14px] font-bold">Số lượng:</p>
              <div className="flex h-10 w-fit border border-brand-line">
                <button className="h-10 w-10" onClick={() => setQty(Math.max(1, qty - 1))} type="button">-</button>
                <span className="grid h-10 w-12 place-items-center border-x border-brand-line">{qty}</span>
                <button className="h-10 w-10" onClick={() => setQty(qty + 1)} type="button">+</button>
              </div>
            </div>

            <div className="mt-8 flex gap-4 max-[520px]:grid">
              <Link className="btn-white border-brand-red text-brand-red" href="/gio-hang">Thêm vào giỏ</Link>
              <Link className="btn-red" href="/thanh-toan">Mua ngay</Link>
            </div>
          </div>
        </section>

        <section className="border-y border-brand-line bg-brand-pale py-7">
          <div className="container-beauty grid grid-cols-3 text-center text-[13px] font-bold max-[620px]:grid-cols-1 max-[620px]:gap-4">
            <span>Miễn phí ship<br /><small className="font-normal text-brand-muted">Đơn từ 500k</small></span>
            <span>Đổi trả dễ dàng<br /><small className="font-normal text-brand-muted">Trong 7 ngày</small></span>
            <span>Sản phẩm chính hãng<br /><small className="font-normal text-brand-muted">Cam kết 100%</small></span>
          </div>
        </section>

        <section className="container-beauty py-10">
          <div className="flex justify-center gap-20 border-b border-brand-line pb-5 text-[13px] font-bold uppercase max-[520px]:gap-5">
            <span className="text-brand-red">Mô tả</span><span>Đánh giá (128)</span><span>Hướng dẫn sử dụng</span>
          </div>
          <div className="mt-8 grid grid-cols-[1fr_0.9fr] gap-8 max-[820px]:grid-cols-1">
            <p className="text-[14px] leading-7 text-brand-muted">{detail?.description ?? "Thông tin sản phẩm đang được cập nhật."}</p>
            <ol className="grid gap-3 text-[14px] text-brand-muted">
              {(detail?.usage ?? []).map((item, index) => <li key={item}><strong className="text-brand-red">{index + 1}.</strong> {item}</li>)}
            </ol>
          </div>
        </section>
      </main>
      <Footer />
    </>
  );
}
