import { BookingForm } from "@/components/BookingForm";
import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { iconBase } from "@/lib/data";

export default function BookingPage() {
  return (
    <>
      <Header />
      <main className="bg-white">
        <section className="w-full px-4 pt-4">
          <div className="mx-auto w-full max-w-[1600px] overflow-hidden rounded-[32px] shadow-[0_14px_34px_rgba(217,46,85,0.08)]">
            <img src="/images/products/6_banner_net_hon/04_banner_dat_lich.png" alt="Đặt lịch makeup" className="block h-auto w-full" />
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
