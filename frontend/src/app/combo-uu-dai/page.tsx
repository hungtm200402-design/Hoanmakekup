import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { iconBase } from "@/lib/data";

const combos = [
  {
    tag: "BEST SELLER",
    title: "Combo Trang Điểm Tự Nhiên",
    desc: "Nền mịn - Môi xinh - Tự tin tỏa sáng",
    old: "1.320.000đ",
    price: "990.000đ",
    save: "Tiết kiệm: 330.000đ",
    image: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/13_combo_trang_diem_tu_nhien.png"
  },
  {
    title: "Combo Dưỡng Sáng Da",
    desc: "Sáng khỏe - Mịn màng - Rạng rỡ",
    old: "2.130.000đ",
    price: "1.490.000đ",
    save: "Tiết kiệm: 640.000đ",
    image: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/14_combo_duong_sang_da.png"
  },
  {
    title: "Combo Trang Điểm Toàn Diện",
    desc: "Đầy đủ - Tiện lợi - Nâng tầm nhan sắc",
    old: "1.850.000đ",
    price: "1.480.000đ",
    save: "Tiết kiệm: 370.000đ",
    image: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/15_combo_trang_diem_toan_dien.png"
  },
  {
    title: "Combo Phục Hồi & Cấp Ẩm",
    desc: "Phục hồi sâu - Cấp ẩm vượt trội",
    old: "2.460.000đ",
    price: "1.590.000đ",
    save: "Tiết kiệm: 870.000đ",
    image: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/16_combo_phuc_hoi_cap_am.png"
  }
];

export default function ComboPage() {
  return (
    <>
      <Header />
      <main className="pb-10">
        <section className="w-full px-4 pt-4">
          <div className="mx-auto w-full max-w-[1600px] overflow-hidden rounded-[32px] shadow-[0_14px_34px_rgba(217,46,85,0.08)]">
            <img src="/images/products/6_banner_net_hon/03_banner_combo_uu_dai.png" alt="Combo ưu đãi" className="block h-auto w-full" />
          </div>
        </section>

        <section className="container-beauty my-6 flex justify-center gap-6 max-[760px]:grid max-[760px]:grid-cols-1">
          {[
            ["Combo makeup", `${iconBase}/01_icon_menu/03_san_pham.png`, true],
            ["Combo chăm sóc da", `${iconBase}/03_icon_thao_tac_san_pham/09_chai_my_pham.png`],
            ["Combo quà tặng", `${iconBase}/01_icon_menu/04_combo_uu_dai.png`]
          ].map(([label, icon, active]) => (
            <button
              key={label as string}
              className={`flex min-h-14 min-w-[220px] items-center justify-center gap-3 rounded-[10px] border px-8 text-[14px] font-extrabold uppercase ${
                active ? "border-brand-red bg-brand-pale text-brand-red" : "border-brand-line bg-white text-brand-ink"
              }`}
              type="button"
            >
              <img src={icon as string} alt="" className="h-7 w-7 object-contain" /> {label}
            </button>
          ))}
        </section>

        <section className="container-beauty grid grid-cols-4 gap-5 max-[1120px]:grid-cols-2 max-[620px]:grid-cols-1">
          {combos.map((combo) => (
            <article
              key={combo.title}
              className="relative flex h-full flex-col overflow-hidden rounded-[10px] border border-[#f5cfd8] bg-white p-4 shadow-[0_10px_26px_rgba(217,46,85,0.06)]"
            >
              <div className="relative flex h-[280px] items-center justify-center overflow-hidden rounded-[8px] bg-brand-pale">
                <img src={combo.image} alt={combo.title} className="h-full w-full object-contain p-5" />
                {combo.tag ? <span className="absolute left-4 top-4 rounded bg-brand-red px-4 py-2 text-[14px] font-bold text-white">{combo.tag}</span> : null}
              </div>

              <div className="flex flex-1 flex-col px-1 pt-5">
                <h2 className="min-h-[56px] text-[18px] font-extrabold leading-snug">{combo.title}</h2>
                <p className="mt-2 min-h-[42px] text-[14px] leading-6 text-brand-muted">{combo.desc}</p>
                <p className="mt-5 text-[15px] text-brand-muted line-through">{combo.old}</p>
                <p className="mt-1 text-[28px] font-extrabold text-brand-red">{combo.price}</p>
                <p className="mt-3 inline-block rounded bg-brand-pale px-3 py-1 text-[13px] font-semibold text-brand-red">{combo.save}</p>
                <button className="mt-auto flex min-h-12 w-full items-center justify-center gap-3 rounded-md bg-brand-red text-[18px] font-bold text-white" type="button">
                  Mua ngay <img src={`${iconBase}/04_icon_tai_khoan_lien_he/16_tui_mua_sam.png`} alt="" className="h-6 w-6 brightness-0 invert" />
                </button>
              </div>
            </article>
          ))}
        </section>
      </main>
      <Footer />
    </>
  );
}
