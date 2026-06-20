import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { ServiceCard } from "@/components/ServiceCard";
import { iconBase, services } from "@/lib/data";

const categories = [
  ["Tất cả dịch vụ", "10"],
  ["Makeup cô dâu", "3"],
  ["Makeup dự tiệc", "2"],
  ["Makeup tại nhà", "2"],
  ["Makeup chụp ảnh", "2"],
  ["Makeup sự kiện", "1"]
];

export default function ServicesPage() {
  return (
    <>
      <Header />
      <main className="bg-white">
        <section className="relative overflow-hidden bg-gradient-to-r from-white via-[#fff4f7] to-white">
          <div className="container-beauty relative grid min-h-[190px] place-items-center text-center">
            <img src="/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/06_home_bo_co_trang_diem_cao_cap.png" alt="" className="absolute -left-16 top-4 h-40 w-40 rotate-[-16deg] object-contain opacity-70 max-[760px]:hidden" />
            <img src="/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/05_home_son_li_hoan_doan.png" alt="" className="absolute -right-10 top-4 h-44 w-44 rotate-12 object-contain opacity-70 max-[760px]:hidden" />
            <div>
              <h1 className="font-serif text-[46px] font-bold uppercase tracking-wide text-brand-red max-[640px]:text-[34px]">Dịch vụ makeup</h1>
              <div className="mx-auto mt-4 h-[2px] w-28 bg-[#d9a269]" />
              <p className="mt-4 text-[15px] text-brand-muted">Tôn vinh vẻ đẹp tự nhiên, tỏa sáng theo cách riêng của bạn</p>
            </div>
          </div>
        </section>

        <section className="container-beauty py-6 text-[13px] text-brand-muted">
          <span className="text-brand-red">Trang chủ</span> › Dịch vụ
        </section>

        <section className="container-beauty grid grid-cols-[280px_1fr] gap-7 pb-12 max-[980px]:grid-cols-1">
          <aside className="h-fit rounded-[10px] border border-[#f5cfd8] bg-white p-5 shadow-[0_10px_26px_rgba(217,46,85,0.06)]">
            <h2 className="font-serif text-[19px] font-bold uppercase text-brand-red">Danh mục dịch vụ</h2>
            <div className="mt-4 border-b border-brand-line pb-5">
              {categories.map(([name, count], index) => (
                <p key={name} className={`flex justify-between rounded-md px-3 py-3 text-[14px] ${index === 0 ? "bg-brand-pale font-bold text-brand-red" : "text-brand-ink"}`}>
                  <span>{name}</span><span className="text-brand-red">{count}</span>
                </p>
              ))}
            </div>

            <div className="border-b border-brand-line py-5">
              <h3 className="font-serif text-[18px] font-bold uppercase text-brand-red">Khoảng giá</h3>
              {["Dưới 1.000.000đ", "1.000.000đ - 2.000.000đ", "2.000.000đ - 3.000.000đ", "Trên 3.000.000đ"].map((item) => (
                <p key={item} className="mt-4 flex items-center gap-3 text-[14px] text-brand-muted"><span className="h-5 w-5 rounded-full border border-[#f5c0cd]" />{item}</p>
              ))}
            </div>

            <div className="py-5">
              <h3 className="font-serif text-[18px] font-bold uppercase text-brand-red">Thời gian</h3>
              {["Dưới 1 giờ", "1 - 2 giờ", "2 - 3 giờ", "Trên 3 giờ"].map((item) => (
                <p key={item} className="mt-4 flex items-center gap-3 text-[14px] text-brand-muted"><span className="h-5 w-5 border border-[#f5c0cd]" />{item}</p>
              ))}
            </div>

            <button className="grid min-h-11 w-full place-items-center rounded-md border border-brand-red text-[14px] font-bold uppercase text-brand-red" type="button">
              ↻ Xóa bộ lọc
            </button>
          </aside>

          <div className="grid grid-cols-3 gap-6 max-[1180px]:grid-cols-2 max-[640px]:grid-cols-1">
            {services.map((service) => <ServiceCard key={service.title} {...service} />)}
          </div>
        </section>

        <section className="container-beauty mb-8 grid grid-cols-4 rounded-[10px] border border-[#f5cfd8] bg-brand-pale px-6 py-5 max-[900px]:grid-cols-2 max-[560px]:grid-cols-1">
          {[
            ["Sản phẩm chất lượng", "Cam kết chính hãng 100%", `${iconBase}/02_icon_dich_vu_uy_tin/15_chung_nhan.png`],
            ["Tư vấn tận tâm", "Hỗ trợ 24/7", `${iconBase}/02_icon_dich_vu_uy_tin/14_tu_van.png`],
            ["An toàn lành tính", "Phù hợp mọi loại da", `${iconBase}/02_icon_dich_vu_uy_tin/06_khien_an_toan.png`],
            ["Đặt lịch dễ dàng", "Nhanh chóng, tiện lợi", `${iconBase}/02_icon_dich_vu_uy_tin/13_giao_hang.png`]
          ].map(([title, text, icon]) => (
            <div key={title} className="flex items-center gap-4 border-r border-brand-line px-5 last:border-r-0 max-[560px]:border-b max-[560px]:border-r-0 max-[560px]:py-3">
              <img src={icon} alt="" className="h-12 w-12 object-contain" />
              <div><p className="text-[14px] font-extrabold uppercase">{title}</p><p className="mt-1 text-[13px] text-brand-muted">{text}</p></div>
            </div>
          ))}
        </section>
      </main>
      <Footer />
    </>
  );
}
