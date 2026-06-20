import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { iconBase } from "@/lib/data";

const combos = [
  { badge: "-25%", tag: "BEST SELLER", title: "Combo Trang Điểm Tự Nhiên", desc: "Nền mịn - Môi xinh - Tự tin tỏa sáng", old: "1.320.000đ", price: "990.000đ", save: "Tiết kiệm: 330.000đ", image: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/13_combo_trang_diem_tu_nhien.png" },
  { badge: "-30%", title: "Combo Dưỡng Sáng Da", desc: "Sáng khỏe - Mịn màng - Rạng rỡ", old: "2.130.000đ", price: "1.490.000đ", save: "Tiết kiệm: 640.000đ", image: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/14_combo_duong_sang_da.png" },
  { badge: "-20%", title: "Combo Trang Điểm Toàn Diện", desc: "Đầy đủ - Tiện lợi - Nâng tầm nhan sắc", old: "1.850.000đ", price: "1.480.000đ", save: "Tiết kiệm: 370.000đ", image: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/15_combo_trang_diem_toan_dien.png" },
  { badge: "-35%", title: "Combo Phục Hồi & Cấp Ẩm", desc: "Phục hồi sâu - Cấp ẩm vượt trội", old: "2.460.000đ", price: "1.590.000đ", save: "Tiết kiệm: 870.000đ", image: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/16_combo_phuc_hoi_cap_am.png" }
];

export default function ComboPage() {
  return (
    <>
      <Header />
      <main className="container-beauty pb-10">
        <section className="relative overflow-hidden rounded-[10px] bg-gradient-to-r from-[#ffe0ea] via-[#fff3f6] to-[#ffd5e0] px-16 py-16 max-[760px]:px-7">
          <div className="relative z-10 max-w-[560px] text-center">
            <div className="mx-auto mb-5 h-[2px] w-56 bg-[#d9a269]" />
            <h1 className="font-serif text-[58px] font-bold uppercase text-brand-red max-[760px]:text-[38px]">Combo ưu đãi</h1>
            <div className="mx-auto mt-4 h-[2px] w-56 bg-[#d9a269]" />
            <p className="mt-6 text-[17px] leading-8">Tiết kiệm hơn - Làm đẹp hơn<br />Cùng những combo được chọn lọc dành riêng cho bạn</p>
          </div>
          <img src="/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/17_banner_combo_uu_dai_san_pham.png" alt="" className="absolute inset-y-0 right-0 h-full w-[52%] object-cover object-center max-[860px]:hidden" />
        </section>

        <section className="my-6 flex justify-center gap-6 max-[760px]:grid max-[760px]:grid-cols-1">
          {[
            ["Combo makeup", `${iconBase}/01_icon_menu/03_san_pham.png`, true],
            ["Combo chăm sóc da", `${iconBase}/03_icon_thao_tac_san_pham/09_chai_my_pham.png`],
            ["Combo quà tặng", `${iconBase}/01_icon_menu/04_combo_uu_dai.png`]
          ].map(([label, icon, active]) => (
            <button key={label as string} className={`flex min-h-14 min-w-[220px] items-center justify-center gap-3 rounded-[10px] border px-8 text-[14px] font-extrabold uppercase ${active ? "border-brand-red bg-brand-pale text-brand-red" : "border-brand-line bg-white text-brand-ink"}`} type="button">
              <img src={icon as string} alt="" className="h-7 w-7 object-contain" /> {label}
            </button>
          ))}
        </section>

        <section className="grid grid-cols-4 gap-5 max-[1120px]:grid-cols-2 max-[620px]:grid-cols-1">
          {combos.map((combo) => (
            <article key={combo.title} className="relative overflow-hidden rounded-[10px] border border-[#f5cfd8] bg-white p-4 shadow-[0_10px_26px_rgba(217,46,85,0.06)]">
              <span className="absolute left-4 top-4 z-10 bg-brand-red px-3 py-1 text-[22px] font-extrabold text-white">{combo.badge}</span>
              <div className="relative grid h-[270px] place-items-center rounded-[8px] bg-brand-pale">
                <img src={combo.image} alt={combo.title} className="h-full w-full object-contain p-6" />
                {combo.tag ? <span className="absolute bottom-4 rounded bg-brand-red px-9 py-2 text-[18px] font-bold text-white">{combo.tag}</span> : null}
              </div>
              <h2 className="mt-5 text-[18px] font-extrabold">{combo.title}</h2>
              <p className="mt-2 text-[14px] text-brand-muted">{combo.desc}</p>
              <p className="mt-5 text-[15px] text-brand-muted line-through">{combo.old}</p>
              <p className="mt-1 text-[28px] font-extrabold text-brand-red">{combo.price}</p>
              <p className="mt-3 inline-block rounded bg-brand-pale px-3 py-1 text-[13px] font-semibold text-brand-red">{combo.save}</p>
              <button className="mt-5 flex min-h-12 w-full items-center justify-center gap-3 rounded-md bg-brand-red text-[18px] font-bold text-white" type="button">
                Mua ngay <img src={`${iconBase}/04_icon_tai_khoan_lien_he/16_tui_mua_sam.png`} alt="" className="h-6 w-6 brightness-0 invert" />
              </button>
            </article>
          ))}
        </section>
      </main>
      <Footer />
    </>
  );
}
