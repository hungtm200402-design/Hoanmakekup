import Link from "next/link";

export function ServiceCard({ slug, title, price, image, desc, duration }: { slug: string; title: string; price: string; image: string; icon?: string; desc?: string; duration?: string }) {
  return (
    <article className="group relative flex h-full flex-col overflow-hidden rounded-[10px] border border-[#f6cfd8] bg-white text-center shadow-[0_10px_26px_rgba(217,46,85,0.06)] transition duration-300 hover:-translate-y-1 hover:shadow-[0_18px_38px_rgba(217,46,85,0.12)]">
      <div className="flex h-[292px] items-center justify-center bg-gradient-to-br from-[#fff4f6] to-white p-5 max-[760px]:h-[240px]">
        {image ? (
          <img className="h-full w-full object-contain object-center transition duration-300 group-hover:scale-[1.03]" src={image} alt={title} />
        ) : (
          <div className="grid h-full w-full place-items-center text-[12px] font-semibold uppercase text-brand-red/55">Khung ảnh dịch vụ</div>
        )}
      </div>
      <div className="px-5 pb-5 pt-5">
        <h3 className="text-[18px] font-extrabold uppercase leading-snug text-brand-ink">{title}</h3>
        <p className="mx-auto mt-3 min-h-[44px] max-w-[260px] text-[14px] leading-6 text-brand-muted">{desc ?? "Dịch vụ làm đẹp chuyên nghiệp, phù hợp với phong cách riêng của bạn."}</p>
        <p className="mt-4 text-[22px] font-extrabold text-brand-red">{price}</p>
        <p className="mt-3 text-[13px] text-brand-ink">• {duration ?? "1.5 - 2 giờ"}</p>
        <div className="mt-5 grid grid-cols-2 gap-3">
          <Link href={`/dich-vu-makeup/${slug}`} className="grid min-h-10 place-items-center rounded-md border border-brand-red bg-white text-[13px] font-bold text-brand-red">
            Xem chi tiết
          </Link>
          <Link href="/dat-lich" className="grid min-h-10 place-items-center rounded-md bg-brand-red text-[13px] font-bold text-white">
            Đặt lịch
          </Link>
        </div>
      </div>
    </article>
  );
}
