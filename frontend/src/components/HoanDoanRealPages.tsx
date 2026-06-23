import Link from "next/link";
import type { ReactNode } from "react";
import { Header } from "@/components/Header";
import { services } from "@/lib/data";

const assetRoot = "/images/products/HoanDoan_Assets_TheoTungTrang_Gon/HoanDoan_Assets_TheoTungTrang_Gon";
const backRoot = "/images/products/hoan_doan_back_final/hoan_doan_back_final";
const homeFrameBack = `${assetRoot}/01_trang_chu/assets/01_trang_chu_khung_trong.png`;
const homeIconRoot = "/images/products/icon_trang_chu/cut";

const trustItems = [
  ["SẢN PHẨM CHÍNH HÃNG", "100% chính hãng, an toàn, lành tính cho da", "shield.png"],
  ["CÔNG NGHỆ HIỆN ĐẠI", "Ứng dụng công nghệ tiên tiến, đạt chuẩn quốc tế", "beaker.png"],
  ["CHUYÊN GIA HÀNG ĐẦU", "Đội ngũ chuyên môn cao, giàu kinh nghiệm", "expert.png"],
  ["KHÔNG GIAN SANG TRỌNG", "Riêng tư & thư giãn, đẳng cấp 5 sao", "diamond.png"],
  ["CAM KẾT HIỆU QUẢ", "Hiệu quả rõ rệt, hài lòng tuyệt đối", "heart.png"]
];

const homeServices = [
  ["CHĂM SÓC DA CHUYÊN SÂU", "Liệu trình cá nhân hóa\nPhục hồi & trẻ hóa da", `${assetRoot}/01_trang_chu/assets/dich_vu_cham_soc_da.png`],
  ["PHUN XĂM THẨM MỸ", "Dáng mày tự nhiên\nMôi xinh chuẩn phong thủy", `${assetRoot}/01_trang_chu/assets/dich_vu_phun_xam.png`],
  ["BODY & RELAX", "Thư giãn - Thải độc tế bào\nTái tạo năng lượng", `${assetRoot}/01_trang_chu/assets/dich_vu_body_relax.png`]
];

const products = [
  ["HD GLOW SERUM", "Dưỡng sáng - Căng bóng - Mờ thâm", "850.000đ", "1.100.000đ", `${assetRoot}/03_san_pham/assets/glow_serum.png`, "BEST SELLER"],
  ["HD PREMIUM TONER", "Cân bằng pH - Cấp ẩm - Làm dịu da", "650.000đ", "850.000đ", `${assetRoot}/03_san_pham/assets/premium_toner.png`, "MỚI"],
  ["HD PERFECT CREAM", "Dưỡng ẩm sâu - Phục hồi - Trẻ hóa da", "950.000đ", "1.200.000đ", `${assetRoot}/03_san_pham/assets/perfect_cream.png`, "BEST SELLER"],
  ["HD REPAIR ESSENCE", "Phục hồi - Tái tạo - Tăng đề kháng da", "750.000đ", "950.000đ", `${assetRoot}/03_san_pham/assets/repair_essence.png`, "MỚI"],
  ["HD LUXURY LIPSTICK", "Lên màu chuẩn - Lì mượt - Không khô", "450.000đ", "530.000đ", `${assetRoot}/03_san_pham/assets/luxury_lipstick.png`, "SALE 15%"],
  ["HD GLOW CUSHION", "Che phủ tự nhiên - Căng bóng - SPF 50+", "650.000đ", "820.000đ", `${assetRoot}/03_san_pham/assets/glow_cushion.png`, "MỚI"]
];

const homeFeaturedProducts = [
  ["HD Glow Serum", "850.000đ", `${assetRoot}/01_trang_chu/assets/san_pham_glow_serum.png`],
  ["HD Perfect Cream", "950.000đ", `${assetRoot}/01_trang_chu/assets/san_pham_perfect_cream.png`],
  ["HD Premium Toner", "650.000đ", `${assetRoot}/01_trang_chu/assets/san_pham_premium_toner.png`]
];

const combos = [
  ["COMBO GLOW TOÀN DIỆN", "Sáng da - Cấp ẩm - Trẻ hóa", "1.990.000đ", "2.850.000đ", "-30%", `${assetRoot}/04_combo_uu_dai/assets/combo_glow_toan_dien.png`],
  ["COMBO CÔ DÂU", "Rạng rỡ trong ngày trọng đại", "2.050.000đ", "3.150.000đ", "-35%", `${assetRoot}/04_combo_uu_dai/assets/combo_co_dau.png`],
  ["COMBO PHỤC HỒI DA", "Phục hồi - Dưỡng ẩm - Bảo vệ", "2.030.000đ", "2.900.000đ", "-30%", `${assetRoot}/04_combo_uu_dai/assets/combo_phuc_hoi_da.png`],
  ["COMBO MAKEUP PARTY", "Tự tin tỏa sáng mọi khoảnh khắc", "1.790.000đ", "2.400.000đ", "-25%", `${assetRoot}/04_combo_uu_dai/assets/combo_makeup_party.png`]
];

export function TrustStrip({ framed = true }: { framed?: boolean }) {
  return (
    <footer className={`${framed ? "home-footer-glass shadow-[0_12px_30px_rgba(190,70,88,0.12)]" : "home-footer-content"} mx-auto mt-4 grid w-[min(100%-48px,1720px)] grid-cols-5 rounded-[14px] px-6 py-4 max-[980px]:grid-cols-2 max-[560px]:grid-cols-1`}>
      {trustItems.map(([title, text, icon]) => (
        <div key={title} className="flex items-center gap-4 border-r border-[#e9bfc4] px-4 last:border-r-0 max-[980px]:border-b max-[560px]:border-r-0">
          <img src={`${homeIconRoot}/${icon}`} alt="" className="h-11 w-11 object-contain" />
          <div>
            <p className="text-[13px] font-bold text-[#933c36]">{title}</p>
            <p className="mt-1 text-[12px] leading-5 text-[#70423e]">{text}</p>
          </div>
        </div>
      ))}
    </footer>
  );
}

function Hero({ bg, children, imageClassName = "block w-full" }: { bg: string; children?: ReactNode; imageClassName?: string }) {
  return (
    <section className="relative mx-auto w-full max-w-[1920px]">
      <img src={bg} alt="" className={imageClassName} />
      {children}
    </section>
  );
}

function PinkButton({ href, children }: { href: string; children: React.ReactNode }) {
  return (
    <Link href={href} className="inline-flex min-h-11 items-center justify-center rounded-lg bg-[#e92f6a] px-8 text-[14px] font-bold uppercase text-white shadow-[0_10px_24px_rgba(221,45,93,0.25)]">
      {children} <span className="ml-5 text-xl leading-none">→</span>
    </Link>
  );
}

export function HomeRealPage() {
  return (
    <div className="home-page">
      <main className="home-stage mx-auto" style={{ backgroundImage: `url(${homeFrameBack})` }}>
        <section className="hero-section relative mx-auto w-full max-w-[1920px] bg-transparent">
          <Header embedded bare />
          <div className="home-hero-copy max-[760px]:hidden">
            <p className="home-hero-eyebrow">Nâng tầm vẻ đẹp Việt</p>
            <h1>Tỏa sáng<br />vẻ đẹp tự tin</h1>
            <p className="home-hero-desc">Hoàn Doãn Beauty &amp; Academy đồng hành cùng bạn<br />trên hành trình chạm đến vẻ đẹp rạng ngời và tự tin mỗi ngày.</p>
            <div className="home-hero-actions">
              <PinkButton href="/dich-vu-makeup">Khám phá ngay</PinkButton>
              <Link href="/dat-lich" className="home-consult-button">
                Đặt lịch tư vấn
                <img src={`${homeIconRoot}/calendar_small.png`} alt="" />
              </Link>
            </div>
          </div>
          <div className="home-vip-panel max-[900px]:hidden">
            <img src={`${homeIconRoot}/vip_crown.png`} alt="" className="home-vip-crown" />
            <strong>VIP PRO MAX</strong>
            <span>Đẳng cấp khác biệt</span>
            <i />
            <div className="home-vip-row"><img src={`${homeIconRoot}/vip_4k.png`} alt="" /><span>4K QUALITY<br />ULTRA HD</span></div>
            <i />
            <div className="home-vip-row"><img src={`${homeIconRoot}/vip_60fps.png`} alt="" /><span>SMOOTH MOTION<br />60 FPS</span></div>
            <i />
            <div className="home-vip-row"><img src={`${homeIconRoot}/vip_user_round.png`} alt="" /><span>PREMIUM UI/UX<br />GLASS MORPHISM</span></div>
          </div>
          <img
            src="/images/products/mau_web/hd_skincare_no_background.png"
            alt="Combo Glow Toàn Diện"
            className="home-hero-combo max-[900px]:hidden"
          />
        </section>

        <section className="home-main-sections relative z-10 mx-auto grid grid-cols-4">
          <article className="home-frame-content flex flex-col rounded-[14px] p-5">
            <h2 className="home-card-title with-icon text-center font-serif text-[28px] uppercase text-[#9d3c35]">
              <img src={`${homeIconRoot}/lotus.png`} alt="" />
              Dịch vụ nổi bật
            </h2>
            <p className="mb-3 text-center text-[13px] text-[#70423e]">Chăm sóc chuyên sâu - Chuẩn y khoa</p>
            {homeServices.map(([title, text, image]) => (
              <div key={title} className="home-service-row mb-2 grid grid-cols-[36%_1fr] overflow-hidden rounded-lg border border-[#efc5ca] bg-[rgba(255,245,242,0.78)]">
                <img src={image} alt={title} className="h-full w-full object-cover" />
                <div className="relative p-2">
                  <h3 className="text-[16px] font-semibold text-[#5d2f2f]">{title}</h3>
                  <p className="whitespace-pre-line text-[12px] leading-4 text-[#70423e]">{text}</p>
                  <Link href="/dich-vu-makeup" className="home-row-arrow" aria-label={`Xem ${title}`}>
                    <img src={`${homeIconRoot}/chevron_right.png`} alt="" />
                  </Link>
                </div>
              </div>
            ))}
            <Link href="/dich-vu-makeup" className="mt-auto flex min-h-10 items-center justify-center rounded-lg border border-[#efbdc5] text-[15px] font-semibold uppercase text-[#d92e55]">Xem tất cả dịch vụ →</Link>
          </article>

          <article className="home-frame-content flex flex-col rounded-[14px] p-5">
            <h2 className="text-center font-serif text-[28px] uppercase text-[#9d3c35]">Sản phẩm bán chạy</h2>
            <p className="mb-4 text-center text-[13px] text-[#70423e]">Tinh túy từ thiên nhiên - Khoa học tiên tiến</p>
            <div className="grid flex-1 grid-cols-3 gap-3 overflow-hidden">
              {homeFeaturedProducts.map(([title, price, image], index) => (
                <div key={title} className="home-product-card flex h-full flex-col overflow-hidden rounded-lg border border-[#efc5ca] text-center">
                  {index === 1 ? <span className="home-product-badge">BEST SELLER</span> : null}
                  <div className="home-product-image flex flex-1 items-center justify-center bg-transparent">
                    <img src={image} alt={title} className="h-full w-full object-contain" />
                  </div>
                  <p className="px-2 pt-3 text-[14px] leading-5">{title.replace("HD ", "HD ")}</p>
                  <strong className="text-[16px] text-[#33211f]">{price}</strong>
                </div>
              ))}
            </div>
            <Link href="/shop-my-pham" className="mt-4 flex min-h-10 items-center justify-center rounded-lg border border-[#efbdc5] text-[15px] font-semibold uppercase text-[#d92e55]">Xem tất cả sản phẩm →</Link>
          </article>

          <article className="home-frame-content flex flex-col rounded-[14px] p-5 text-center">
            <h2 className="font-serif text-[28px] uppercase text-[#9d3c35]">Combo ưu đãi</h2>
            <p className="mb-3 text-[13px] text-[#70423e]">Hiệu quả nhân đôi - Tiết kiệm tối ưu</p>
            <div className="home-combo-inner">
              <div className="home-combo-image relative flex items-center justify-center overflow-hidden rounded-lg bg-[rgba(255,245,242,0.78)]">
                <span className="absolute left-1/2 top-2 z-10 -translate-x-1/2 rounded-full bg-[#e98663] px-3 py-0.5 text-[9px] font-bold leading-4 text-white">BEST DEAL</span>
                <img src={`${assetRoot}/01_trang_chu/assets/combo_glow_toan_dien.png`} alt="Combo Glow Toàn Diện" className="h-full w-full object-cover" />
              </div>
              <h3 className="font-semibold">COMBO GLOW TOÀN DIỆN</h3>
              <p className="text-[13px] text-[#70423e]">Sáng da - Cấp ẩm - Trẻ hóa</p>
              <p className="home-combo-price"><strong>1.990.000đ</strong> <span>2.880.000đ</span></p>
              <p className="home-combo-save">TIẾT KIỆM 31%</p>
            </div>
            <Link href="/combo-uu-dai" className="mt-auto flex min-h-10 items-center justify-center rounded-lg border border-[#efbdc5] text-[15px] font-semibold uppercase text-[#d92e55]">Khám phá combo →</Link>
          </article>

          <article className="home-frame-content flex flex-col rounded-[14px] p-5">
            <h2 className="text-center font-serif text-[28px] uppercase text-[#9d3c35]">Đặt lịch dễ dàng</h2>
            <p className="mb-4 text-center text-[13px] text-[#70423e]">Chọn dịch vụ - Chọn thời gian - Xác nhận</p>
            {[
              ["Chọn dịch vụ", "vip_shield_round.png", "chevron_down_1.png"],
              ["Chọn chuyên viên", "vip_user_round.png", "chevron_down_1.png"],
              ["Ngày mong muốn", "calendar.png", "calendar_small.png"],
              ["Thời gian", "clock.png", ""]
            ].map(([item, leftIcon, rightIcon]) => (
              <div key={item} className="home-booking-field mb-4 rounded-lg border border-[#efc5ca] bg-[rgba(255,245,242,0.78)] px-4 py-4 text-[14px] text-[#70423e]">
                <img src={`${homeIconRoot}/${leftIcon}`} alt="" />
                <span>{item}</span>
                {rightIcon ? <img src={`${homeIconRoot}/${rightIcon}`} alt="" /> : null}
              </div>
            ))}
            <Link href="/dat-lich" className="flex min-h-12 items-center justify-center rounded-lg bg-[#e92f6a] text-[17px] font-bold uppercase text-white">Đặt lịch ngay</Link>
            <p className="mt-4 text-center text-[14px] text-[#5d2f2f]">Hoặc gọi ngay: 1900 1234</p>
          </article>
        </section>
        <TrustStrip framed={false} />
      </main>
    </div>
  );
}

export function ShopRealPage() {
  return (
    <>
      <main className="bg-[#f8ddd6]">
        <Hero bg={`${backRoot}/02_duong_da_va_trang_diem_cao_cap.png`}>
          <Header embedded />
          <div className="absolute left-[16%] top-[40%] max-[760px]:hidden">
            <PinkButton href="/shop-my-pham">Khám phá ngay</PinkButton>
          </div>
        </Hero>
        <section className="mx-auto grid w-[min(100%-56px,1800px)] grid-cols-[280px_1fr] gap-4 py-4 max-[980px]:grid-cols-1">
          <aside className="rounded-xl border border-[#efbdc5] bg-white/72 p-5 backdrop-blur">
            <h2 className="font-serif text-[21px] uppercase text-[#9d3c35]">Sản phẩm nổi bật</h2>
            {["Tất cả sản phẩm", "Serum", "Cream", "Toner", "Makeup"].map((item, index) => (
              <p key={item} className={`mt-3 rounded-lg border border-[#efc5ca] px-4 py-3 text-[14px] font-semibold ${index === 0 ? "bg-[#ffe5ec] text-[#d92e55]" : "text-[#70423e]"}`}>{item}</p>
            ))}
          </aside>
          <div className="grid grid-cols-3 gap-4 max-[1200px]:grid-cols-2 max-[620px]:grid-cols-1">
            {products.map(([title, desc, price, oldPrice, image, tag]) => <ProductCard key={title} title={title} desc={desc} price={price} oldPrice={oldPrice} image={image} tag={tag} />)}
          </div>
        </section>
        <TrustStrip />
      </main>
    </>
  );
}

function ProductCard({ title, desc, price, oldPrice, image, tag }: Record<string, string>) {
  return (
    <article className="flex h-full flex-col rounded-xl border border-[#efbdc5] bg-white/70 p-4 text-center backdrop-blur">
      <div className="relative overflow-hidden rounded-lg bg-[#fff1ed]">
        <span className="absolute left-4 top-3 z-10 rounded-full bg-[#e92f6a] px-3 py-1 text-[11px] font-bold text-white">{tag}</span>
        <span className="absolute right-4 top-3 z-10 text-2xl text-[#d92e55]">♡</span>
        <img src={image} alt={title} className="h-[255px] w-full object-cover" />
      </div>
      <p className="mt-3 text-[13px] text-[#c38345]">★★★★★ 4.9</p>
      <h2 className="mt-1 font-semibold text-[#33211f]">{title}</h2>
      <p className="mt-1 text-[13px] text-[#70423e]">{desc}</p>
      <p className="mt-3"><strong className="text-[20px]">{price}</strong> <span className="text-[13px] text-[#9c8884] line-through">{oldPrice}</span></p>
      <Link href="/gio-hang" className="mt-auto flex min-h-10 items-center justify-center rounded-lg border border-[#efbdc5] font-semibold uppercase text-[#d92e55]">Thêm vào giỏ</Link>
    </article>
  );
}

export function ComboRealPage() {
  return (
    <>
      <main className="bg-[#f8ddd6]">
        <Hero bg={`${backRoot}/03_combo_uu_dai_cao_cap.png`}>
          <Header embedded />
          <div className="absolute left-[10.5%] top-[38%] max-[760px]:hidden">
            <PinkButton href="/combo-uu-dai">Khám phá ngay</PinkButton>
          </div>
        </Hero>
        <section className="mx-auto grid w-[min(100%-56px,1720px)] grid-cols-4 gap-4 py-4 max-[1180px]:grid-cols-2 max-[640px]:grid-cols-1">
          {combos.map(([title, desc, price, oldPrice, sale, image]) => (
            <article key={title} className="rounded-xl border border-[#efbdc5] bg-white/72 p-5 backdrop-blur">
              <div className="flex items-start justify-between">
                <div>
                  <h2 className="font-serif text-[22px] uppercase text-[#9d3c35]">{title}</h2>
                  <p className="text-[13px] text-[#70423e]">{desc}</p>
                </div>
                <span className="rounded-full bg-[#e92f6a] px-4 py-2 font-bold text-white">{sale}</span>
              </div>
              <img src={image} alt={title} className="mx-auto my-3 h-[190px] object-contain" />
              <p className="text-right"><span className="mr-4 text-[#6a5653] line-through">{oldPrice}</span><strong className="text-[26px] text-[#d92e55]">{price}</strong></p>
              <Link href="/gio-hang" className="mt-3 flex min-h-11 items-center justify-center rounded-lg bg-[#e92f6a] font-bold uppercase text-white">Mua combo →</Link>
            </article>
          ))}
        </section>
        <TrustStrip />
      </main>
    </>
  );
}

export function ServicesRealPage() {
  return (
    <>
      <main className="bg-white">
        <section className="relative bg-[#fff4f5] px-6 pb-12 pt-[190px] text-center max-[760px]:pt-[120px]">
          <Header embedded />
          <h1 className="font-serif text-[44px] font-bold uppercase text-[#e92f6a]">Dịch vụ makeup</h1>
          <p className="mt-3 text-[#70423e]">Tôn vinh vẻ đẹp tự nhiên, tỏa sáng theo cách riêng của bạn</p>
        </section>
        <section className="container-beauty grid grid-cols-[280px_1fr] gap-7 py-8 max-[980px]:grid-cols-1">
          <aside className="h-fit rounded-xl border border-[#efbdc5] bg-white p-5 shadow-[0_10px_26px_rgba(217,46,85,0.06)]">
            <h2 className="font-serif text-[19px] font-bold uppercase text-[#e92f6a]">Danh mục dịch vụ</h2>
            {["Tất cả dịch vụ", "Makeup cô dâu", "Makeup dự tiệc", "Makeup tại nhà", "Makeup chụp ảnh", "Makeup sự kiện"].map((item, index) => (
              <p key={item} className={`mt-3 rounded-lg px-3 py-3 text-[14px] ${index === 0 ? "bg-[#ffe5ec] font-bold text-[#e92f6a]" : "text-[#70423e]"}`}>{item}</p>
            ))}
          </aside>
          <div className="grid grid-cols-3 gap-6 max-[1180px]:grid-cols-2 max-[640px]:grid-cols-1">
            {services.map((service) => (
              <article key={service.slug} className="overflow-hidden rounded-xl border border-[#efbdc5] bg-white shadow-[0_10px_26px_rgba(217,46,85,0.06)]">
                <img src={service.image} alt={service.title} className="h-[300px] w-full object-cover" />
                <div className="p-5 text-center">
                  <h2 className="font-bold">{service.title}</h2>
                  <p className="mt-2 min-h-12 text-[14px] text-[#70423e]">{service.desc}</p>
                  <strong className="mt-3 block text-[22px] text-[#e92f6a]">{service.price}</strong>
                  <p className="mt-2 text-[13px] text-[#70423e]">{service.duration}</p>
                  <div className="mt-5 grid grid-cols-2 gap-3">
                    <Link href={`/dich-vu-makeup/${service.slug}`} className="rounded-lg border border-[#e92f6a] py-3 text-[13px] font-semibold text-[#e92f6a]">Xem chi tiết</Link>
                    <Link href="/dat-lich" className="rounded-lg bg-[#e92f6a] py-3 text-[13px] font-semibold text-white">Đặt lịch</Link>
                  </div>
                </div>
              </article>
            ))}
          </div>
        </section>
        <TrustStrip />
      </main>
    </>
  );
}

export function BookingRealPage() {
  return (
    <>
      <main className="bg-[#f8ddd6]">
        <Hero bg={`${backRoot}/06_dat_lich_cham_soc_sac_dep.png`}>
          <Header embedded />
        </Hero>
        <section className="mx-auto -mt-16 grid w-[min(100%-56px,1560px)] grid-cols-[1.15fr_1fr] gap-5 pb-4 max-[1000px]:mt-4 max-[1000px]:grid-cols-1">
          <form className="rounded-xl border border-[#efbdc5] bg-white/76 p-6 backdrop-blur">
            <h2 className="font-serif text-[24px] uppercase text-[#9d3c35]">Thông tin đặt lịch</h2>
            <div className="mt-4 grid grid-cols-2 gap-4 max-[640px]:grid-cols-1">
              {["Họ và tên", "Số điện thoại", "Chọn dịch vụ", "Chọn chuyên viên", "Ngày mong muốn", "Thời gian"].map((label) => (
                <label key={label} className="rounded-lg border border-[#efc5ca] bg-white/55 px-4 py-3">
                  <span className="block text-[13px] font-semibold text-[#70423e]">{label}</span>
                  <input className="mt-1 w-full bg-transparent text-[13px] outline-none" placeholder={label.includes("Chọn") ? label : `Nhập ${label.toLowerCase()}`} />
                </label>
              ))}
              <label className="col-span-2 rounded-lg border border-[#efc5ca] bg-white/55 px-4 py-3 max-[640px]:col-span-1">
                <span className="block text-[13px] font-semibold text-[#70423e]">Ghi chú (nếu có)</span>
                <input className="mt-1 w-full bg-transparent text-[13px] outline-none" placeholder="Nhập ghi chú hoặc tình trạng da..." />
              </label>
            </div>
            <button className="mt-4 min-h-14 w-full rounded-lg bg-[#e92f6a] text-[18px] font-bold uppercase text-white" type="button">Gửi đặt lịch →</button>
          </form>
          <section className="grid gap-4">
            <InfoBox title="Chi nhánh Hoàn Doãn" lines={["123 Nguyễn Văn Trỗi, Phường 8, Quận Phú Nhuận, TP.HCM", "1900 1234", "hotro@hoandoanbeauty.vn"]} />
            <InfoBox title="Giờ hoạt động" lines={["Thứ 2 - Thứ 6: 08:30 - 20:00", "Thứ 7 - Chủ nhật: 08:00 - 20:00", "Làm việc tất cả các ngày lễ"]} />
            <InfoBox title="Vì sao chọn Hoàn Doãn?" lines={["Tư vấn miễn phí", "Xác nhận nhanh", "Dịch vụ chuyên nghiệp"]} />
          </section>
        </section>
        <TrustStrip />
      </main>
    </>
  );
}

function InfoBox({ title, lines }: { title: string; lines: string[] }) {
  return (
    <article className="rounded-xl border border-[#efbdc5] bg-white/76 p-6 backdrop-blur">
      <h2 className="font-serif text-[22px] uppercase text-[#9d3c35]">{title}</h2>
      {lines.map((line) => <p key={line} className="mt-3 text-[14px] text-[#70423e]">{line}</p>)}
    </article>
  );
}

export function AccountRealPage() {
  return (
    <>
      <main className="bg-[#f8ddd6]">
        <Hero bg={`${backRoot}/05_tai_khoan_cua_ban.png`}>
          <Header embedded />
        </Hero>
        <section className="mx-auto grid w-[min(100%-56px,1500px)] grid-cols-[240px_1fr] gap-4 py-5 max-[900px]:grid-cols-1">
          <aside className="rounded-xl border border-[#efbdc5] bg-white/76 p-4 backdrop-blur">
            {["Hồ sơ", "Đơn hàng", "Lịch hẹn", "Yêu thích", "Điểm thưởng", "Đổi mật khẩu", "Địa chỉ", "Đăng xuất"].map((item, index) => (
              <p key={item} className={`rounded-lg px-4 py-4 text-[14px] font-semibold ${index === 0 ? "bg-[#e92f6a] text-white" : "text-[#70423e]"}`}>{item}</p>
            ))}
          </aside>
          <section className="grid gap-4">
            <article className="grid grid-cols-[180px_1fr_1fr] gap-6 rounded-xl border border-[#efbdc5] bg-white/76 p-5 backdrop-blur max-[900px]:grid-cols-1">
              <img src={`${assetRoot}/06_tai_khoan/assets/avatar_tai_khoan.png`} alt="Nguyễn Thảo My" className="h-[160px] w-[160px] rounded-full object-cover" />
              <div>
                <p>Xin chào,</p>
                <h1 className="font-serif text-[34px] text-[#33211f]">Nguyễn Thảo My</h1>
                <span className="mt-3 inline-block rounded border border-[#efbdc5] px-4 py-1 text-[13px] text-[#9d3c35]">Thành viên VIP</span>
              </div>
              <div className="grid grid-cols-2 gap-3">
                {["18 Đơn hàng", "18.650.000đ", "12 Lượt", "2.450 Điểm"].map((item) => <div key={item} className="rounded-lg border border-[#efc5ca] p-4 text-[18px] font-semibold">{item}</div>)}
              </div>
            </article>
            <div className="grid grid-cols-3 gap-4 max-[1180px]:grid-cols-1">
              <InfoBox title="Đơn hàng gần đây" lines={["#HD12560 - 2.850.000đ", "#HD12480 - 1.950.000đ", "#HD12420 - 950.000đ"]} />
              <InfoBox title="Lịch hẹn sắp tới" lines={["Chăm sóc da chuyên sâu - 23/05/2025", "Phun xăm thẩm mỹ - 27/05/2025", "Makeup cá nhân - 31/05/2025"]} />
              <InfoBox title="Sản phẩm yêu thích" lines={["HD Perfect Cream - 950.000đ", "HD Glow Serum - 850.000đ", "HD Premium Toner - 650.000đ"]} />
            </div>
            <InfoBox title="Thông tin cá nhân" lines={["Họ và tên: Nguyễn Thảo My", "Số điện thoại: 0901 234 567", "Email: thaomy@gmail.com", "Địa chỉ: 123 Đường Hoa Lan, Phường 2, Quận Phú Nhuận, TP. Hồ Chí Minh"]} />
          </section>
        </section>
        <TrustStrip />
      </main>
    </>
  );
}

export function CartRealPage() {
  return (
    <>
      <main className="bg-[#f8ddd6]">
        <Hero bg={`${backRoot}/04_gio_hang.png`}>
          <Header embedded />
        </Hero>
        <section className="mx-auto grid w-[min(100%-56px,1500px)] grid-cols-[1fr_380px] gap-5 py-5 max-[980px]:grid-cols-1">
          <div className="grid gap-4">
            {[
              ["HD Glow Serum", "850.000đ", `${assetRoot}/07_gio_hang/assets/glow_serum_trong_gio.png`],
              ["HD Perfect Cream", "950.000đ", `${assetRoot}/07_gio_hang/assets/perfect_cream_trong_gio.png`]
            ].map(([title, price, image]) => (
              <article key={title} className="grid grid-cols-[150px_1fr_auto] items-center gap-4 rounded-xl border border-[#efbdc5] bg-white/76 p-4 backdrop-blur max-[640px]:grid-cols-1">
                <img src={image} alt={title} className="h-[140px] object-contain" />
                <div>
                  <h2 className="font-serif text-[24px] text-[#9d3c35]">{title}</h2>
                  <p className="mt-2 text-[14px] text-[#70423e]">Số lượng: 1</p>
                </div>
                <strong className="text-[24px] text-[#d92e55]">{price}</strong>
              </article>
            ))}
          </div>
          <aside className="h-fit rounded-xl border border-[#efbdc5] bg-white/76 p-5 backdrop-blur">
            <h2 className="font-serif text-[24px] uppercase text-[#9d3c35]">Tóm tắt đơn hàng</h2>
            <p className="mt-5 flex justify-between"><span>Tạm tính</span><strong>1.800.000đ</strong></p>
            <p className="mt-3 flex justify-between"><span>Vận chuyển</span><strong>Miễn phí</strong></p>
            <p className="mt-5 flex justify-between border-t border-[#efbdc5] pt-4 text-[20px]"><span>Tổng cộng</span><strong className="text-[#d92e55]">1.800.000đ</strong></p>
            <Link href="/thanh-toan" className="mt-5 flex min-h-12 items-center justify-center rounded-lg bg-[#e92f6a] font-bold uppercase text-white">Thanh toán</Link>
          </aside>
        </section>
        <TrustStrip />
      </main>
    </>
  );
}
