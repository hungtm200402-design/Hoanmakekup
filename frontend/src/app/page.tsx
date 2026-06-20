import Link from "next/link";
import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { ProductGrid } from "@/components/ProductGrid";
import { ServiceCard } from "@/components/ServiceCard";
import { iconBase, services } from "@/lib/data";

const stats = [
  { value: "5,000+", label: "Khách hàng hài lòng", icon: `${iconBase}/02_icon_dich_vu_uy_tin/01_khach_hang.png` },
  { value: "10+", label: "Năm kinh nghiệm", icon: `${iconBase}/02_icon_dich_vu_uy_tin/02_huy_chuong.png` },
  { value: "15+", label: "Chuyên viên chuyên nghiệp", icon: `${iconBase}/02_icon_dich_vu_uy_tin/03_chuyen_vien.png` },
  { value: "100%", label: "Sản phẩm chính hãng", icon: `${iconBase}/02_icon_dich_vu_uy_tin/04_la_tu_nhien.png` }
];

const trustItems = [
  { title: "ĐỘI NGŨ CHUYÊN NGHIỆP", text: "Chuyên viên được đào tạo bài bản, giàu kinh nghiệm và tận tâm.", icon: `${iconBase}/02_icon_dich_vu_uy_tin/05_guong_mat_hoa_sen.png` },
  { title: "SẢN PHẨM CHÍNH HÃNG", text: "Cam kết sử dụng sản phẩm chính hãng, an toàn cho da.", icon: `${iconBase}/02_icon_dich_vu_uy_tin/06_khien_an_toan.png` },
  { title: "CÔNG NGHỆ HIỆN ĐẠI", text: "Ứng dụng công nghệ tiên tiến, hiệu quả và an toàn.", icon: `${iconBase}/02_icon_dich_vu_uy_tin/07_lap_lanh.png` },
  { title: "DỊCH VỤ TẬN TÂM", text: "Tư vấn tận tình, chăm sóc chu đáo, đồng hành cùng bạn.", icon: `${iconBase}/02_icon_dich_vu_uy_tin/08_trai_tim.png` }
];

export default function HomePage() {
  const featuredServices = services.filter((service) => service.slug !== "makeup-chup-anh");

  return (
    <>
      <Header />
      <main className="overflow-hidden bg-[linear-gradient(180deg,#fff7f8_0%,#ffffff_16%,#ffffff_100%)]">
        <section className="w-full bg-[radial-gradient(circle_at_top,#fff0f4_0%,#ffffff_42%)]">
          <div className="mx-auto w-full max-w-[1600px] px-4 pt-4">
            <div className="relative overflow-hidden rounded-[32px] shadow-[0_14px_34px_rgba(217,46,85,0.08)]">
              <img className="block h-auto w-full select-none object-cover" src="/images/products/hoan_doan_mau_make_san_pham_va_banner/03_banner/banner-trang-chu.png" alt="Tỏa sáng vẻ đẹp tự tin là chính bạn" />
              <Link href="/dich-vu-makeup" className="absolute bottom-[12%] left-[11%] inline-flex min-h-[46px] items-center justify-center rounded-full bg-brand-red px-8 text-[14px] font-extrabold uppercase text-white shadow-[0_12px_24px_rgba(217,46,85,0.24)] transition hover:translate-x-1 max-[760px]:bottom-[8%] max-[760px]:left-[7%] max-[760px]:min-h-9 max-[760px]:px-5 max-[760px]:text-[11px]">
                Khám phá ngay →
              </Link>
            </div>
          </div>
        </section>

        <section className="w-full">
          <div className="container-beauty relative z-20 -mt-14 grid grid-cols-4 rounded-[18px] bg-white/92 px-5 py-5 shadow-[0_16px_38px_rgba(217,46,85,0.12)] backdrop-blur max-[900px]:mt-0 max-[900px]:grid-cols-2 max-[520px]:grid-cols-1">
            {stats.map((item) => (
              <div key={item.value} className="flex items-center justify-center gap-4 border-r border-brand-line px-4 last:border-r-0 max-[900px]:border-b max-[900px]:py-3 max-[520px]:justify-start max-[520px]:border-r-0">
                <img src={item.icon} alt="" className="h-10 w-10 object-contain" />
                <div>
                  <p className="text-[22px] font-extrabold text-brand-red">{item.value}</p>
                  <p className="text-[12px] text-brand-ink">{item.label}</p>
                </div>
              </div>
            ))}
          </div>
        </section>

        <section className="w-full py-10">
          <div className="container-beauty grid grid-cols-4 gap-0 max-[980px]:grid-cols-2 max-[560px]:grid-cols-1">
            {trustItems.map((item) => (
              <div key={item.title} className="flex min-h-[112px] items-center gap-5 border-r border-brand-line px-8 last:border-r-0 max-[560px]:border-b max-[560px]:border-r-0 max-[560px]:px-2">
                <img src={item.icon} alt="" className="h-14 w-14 object-contain" />
                <div>
                  <h3 className="text-[13px] font-extrabold">{item.title}</h3>
                  <p className="mt-2 text-[12px] leading-5 text-brand-muted">{item.text}</p>
                </div>
              </div>
            ))}
          </div>
        </section>

        <section className="w-full bg-[#fff8fa] py-12">
          <div className="mx-auto w-full max-w-[1440px] px-6 pb-10 max-[520px]:px-4">
            <div className="mb-8 flex items-end justify-between gap-4">
              <div className="flex-1 text-center">
                <h2 className="section-title">Dịch vụ nổi bật</h2>
                <div className="mx-auto mt-2 h-[2px] w-28 bg-[#d9a269]" />
              </div>
              <Link href="/dich-vu-makeup" className="text-[13px] font-semibold text-brand-red max-[560px]:hidden">Xem tất cả →</Link>
            </div>
            <div className="grid grid-cols-4 gap-6 max-[1024px]:grid-cols-2 max-[560px]:grid-cols-1">
              {featuredServices.map((service) => <ServiceCard key={service.title} {...service} />)}
            </div>
          </div>
        </section>

        <section className="w-full py-12">
          <div className="mx-auto w-full max-w-[1440px] px-6 pb-16 max-[520px]:px-4">
            <div className="mb-8 flex items-end justify-between gap-4">
              <div className="flex-1 text-center">
                <h2 className="section-title">Sản phẩm nổi bật</h2>
                <div className="mx-auto mt-2 h-[2px] w-28 bg-[#d9a269]" />
              </div>
              <Link href="/shop-my-pham" className="text-[13px] font-semibold text-brand-red max-[560px]:hidden">Xem tất cả →</Link>
            </div>
            <ProductGrid limit={6} />
          </div>
        </section>
      </main>
      <Footer />
    </>
  );
}
