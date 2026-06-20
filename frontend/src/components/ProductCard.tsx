import Link from "next/link";
import { iconBase } from "@/lib/data";

type ProductCardProps = {
  id: string;
  name: string;
  price: string;
  oldPrice?: string;
  badge?: string;
  image: string;
  brand?: string;
  compact?: boolean;
};

export function ProductCard({ id, name, price, oldPrice, badge, image, brand = "Hoan Doan", compact = false }: ProductCardProps) {
  return (
    <article className="group relative flex h-full flex-col overflow-hidden rounded-[12px] border border-[#f5cfd8] bg-white shadow-[0_12px_28px_rgba(217,46,85,0.05)] transition hover:-translate-y-1 hover:shadow-[0_18px_38px_rgba(217,46,85,0.12)]">
      {badge ? <span className="absolute left-4 top-4 z-20 rounded-md bg-brand-red px-3 py-1 text-[12px] font-extrabold text-white">{badge}</span> : null}
      <button className="absolute right-4 top-4 z-20 grid h-8 w-8 place-items-center" aria-label="Yeu thich" type="button">
        <img src={`${iconBase}/03_icon_thao_tac_san_pham/01_yeu_thich.png`} alt="" className="h-7 w-7 object-contain opacity-70" />
      </button>
      <Link href={`/shop-my-pham/${id}`} className="block">
        <div className={`relative overflow-hidden rounded-t-[12px] border-b border-[#f8dbe2] bg-[#fff3f6] ${compact ? "h-[270px]" : "h-[320px]"} max-[560px]:h-[240px]`}>
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_50%_36%,rgba(255,255,255,0.98)_0%,rgba(255,247,249,0.92)_46%,rgba(255,230,237,0.98)_100%)]" />
          <div className="absolute inset-0 flex items-center justify-center p-3">
            {image ? (
              <div className="flex h-full w-full items-center justify-center rounded-[24px] border border-white/70 bg-white/25 p-2 shadow-[0_8px_24px_rgba(217,46,85,0.06)]">
                <img className="max-h-full max-w-full object-contain object-center" src={image} alt={name} />
              </div>
            ) : (
              <div className="grid h-full w-full place-items-center rounded-[24px] border border-dashed border-[#f1bac8] text-[12px] font-bold uppercase text-brand-red/50">
                Khung anh san pham
              </div>
            )}
          </div>
        </div>
        <div className="flex flex-1 flex-col px-5 pb-5 pt-4">
          <h3 className="min-h-[44px] text-[15px] font-bold leading-snug text-brand-ink">{name}</h3>
          <p className="mt-1 text-[13px] text-brand-muted">{brand}</p>
          <p className="mt-3 text-[13px] text-[#ff9f19]">★★★★★ <span className="text-brand-muted">(96)</span></p>
          <div className="mt-auto flex items-end justify-between gap-4 pt-4">
            <p className="text-[17px] font-extrabold text-brand-red">
              {price}
              {oldPrice ? <span className="ml-2 text-[12px] font-normal text-brand-muted line-through">{oldPrice}</span> : null}
            </p>
            <span className="grid h-10 w-10 shrink-0 place-items-center rounded-md bg-brand-red">
              <img src={`${iconBase}/03_icon_thao_tac_san_pham/03_them_gio_hang.png`} alt="" className="h-6 w-6 object-contain brightness-0 invert" />
            </span>
          </div>
        </div>
      </Link>
    </article>
  );
}
