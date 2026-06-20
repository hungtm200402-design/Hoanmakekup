import { BookingForm } from "@/components/BookingForm";
import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { iconBase } from "@/lib/data";

export default function BookingPage() {
  return (
    <>
      <Header />
      <main className="bg-white">
        <section className="relative overflow-hidden bg-gradient-to-r from-white via-[#fff4f7] to-white">
          <div className="container-beauty relative grid min-h-[210px] place-items-center text-center">
            <img src="/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/06_home_bo_co_trang_diem_cao_cap.png" alt="" className="absolute -left-20 top-8 h-40 w-40 rotate-[-18deg] object-contain opacity-70 max-[760px]:hidden" />
            <img src="/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/05_home_son_li_hoan_doan.png" alt="" className="absolute -right-10 top-6 h-44 w-44 rotate-12 object-contain opacity-70 max-[760px]:hidden" />
            <div>
              <h1 className="font-serif text-[54px] font-bold uppercase tracking-wide text-brand-red max-[640px]:text-[36px]">Đặt lịch makeup ✦</h1>
              <div className="mx-auto mt-4 h-[2px] w-72 bg-[#d9a269]" />
              <p className="mt-4 text-[15px] text-brand-muted">Đẹp rạng ngời cho mọi khoảnh khắc</p>
            </div>
          </div>
        </section>

        <section className="container-beauty py-8">
          <BookingForm />
        </section>

        <section className="container-beauty mb-8 grid grid-cols-4 rounded-[10px] border border-[#f5cfd8] bg-white px-6 py-5 shadow-[0_10px_26px_rgba(217,46,85,0.06)] max-[900px]:grid-cols-2 max-[560px]:grid-cols-1">
          {[
            ["Cam kết chất lượng", "Sản phẩm chính hãng, an toàn cho da", `${iconBase}/02_icon_dich_vu_uy_tin/06_khien_an_toan.png`],
            ["Đúng giờ", "Đúng hẹn, chuyên nghiệp, không để bạn chờ lâu", `${iconBase}/04_icon_tai_khoan_lien_he/01_dong_ho.png`],
            ["Makeup chuyên nghiệp", "Đội ngũ MUA giàu kinh nghiệm, update xu hướng mới nhất", `${iconBase}/02_icon_dich_vu_uy_tin/11_son_moi.png`],
            ["Tận tâm chu đáo", "Hỗ trợ nhiệt tình trước, trong và sau dịch vụ", `${iconBase}/02_icon_dich_vu_uy_tin/08_trai_tim.png`]
          ].map(([title, text, icon]) => (
            <div key={title} className="flex items-center gap-4 border-r border-brand-line px-5 last:border-r-0 max-[560px]:border-b max-[560px]:border-r-0 max-[560px]:py-3">
              <img src={icon} alt="" className="h-12 w-12 object-contain" />
              <div><p className="text-[14px] font-extrabold uppercase text-brand-red">{title}</p><p className="mt-1 text-[13px] text-brand-muted">{text}</p></div>
            </div>
          ))}
        </section>
      </main>
      <Footer />
    </>
  );
}
