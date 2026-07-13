"use client";

import Link from "next/link";
import { FormEvent, startTransition, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Header } from "@/components/Header";
import { CartAddedToast } from "@/components/CartAddedToast";
import { ApiProduct, fetchProducts, formatVnd } from "@/lib/api";
import { createBookingSubmissionGate, fetchAvailableAppointmentSlots, submitAppointment } from "@/lib/bookingSubmission";
import {
  addCartItem,
  clearCartItems,
  decreaseCartItem,
  getCartSummary,
  getCartUnitPrice,
  increaseCartItem,
  loadCartItems,
  notifyCartChanged,
  reconcileCartItems,
  removeCartItem,
  saveCartItems,
  type CartItem
} from "@/lib/cart";

const assetRoot = "/images/products/HoanDoan_Assets_TheoTungTrang_Gon/HoanDoan_Assets_TheoTungTrang_Gon";
const backRoot = "/images/products/hoan_doan_back_final/hoan_doan_back_final";
const homeFrameBack = `${assetRoot}/01_trang_chu/assets/trang_chu.png`;
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
  ["HD PERFECT CREAM", "Dưỡng ẩm sâu - Phục hồi - Trẻ hóa da", "950.000đ", "1.200.000đ", `${assetRoot}/03_san_pham/assets/perfect_cream.png`, "BEST SELLER"],
  ["HD GLOW SERUM", "Dưỡng sáng - Căng bóng - Mờ thâm", "850.000đ", "1.100.000đ", `${assetRoot}/03_san_pham/assets/glow_serum.png`, "BEST SELLER"],
  ["HD PREMIUM TONER", "Cân bằng pH - Cấp ẩm - Làm dịu da", "650.000đ", "850.000đ", `${assetRoot}/03_san_pham/assets/premium_toner.png`, "MỚI"],
  ["COMBO GLOW TOÀN DIỆN", "Sáng da - Cấp ẩm - Trẻ hóa", "1.990.000đ", "2.850.000đ", `${assetRoot}/04_combo_uu_dai/assets/combo_glow_toan_dien.png`, "BEST SELLER"],
  ["HD CLEANSER", "Làm sạch sâu - Dịu nhẹ - Cấp ẩm", "390.000đ", "520.000đ", `${assetRoot}/03_san_pham/assets/sunscreen_spf50.png`, "MỚI"],
  ["HD LUXURY LIPSTICK", "Lên màu chuẩn - Lì mượt - Không khô", "450.000đ", "530.000đ", `${assetRoot}/03_san_pham/assets/luxury_lipstick.png`, "SALE 15%"],
];

const productTabs = [
  ["Tất cả sản phẩm", "menu_combo.png"],
  ["Serum", "serum.png"],
  ["Cream", "cream.png"],
  ["Toner", "toner_tall.png"],
  ["Makeup", "spray.png"],
  ["Suncare", "vip_4k.png"],
  ["Mask", "vip_user_round.png"],
  ["Bộ sản phẩm", "menu_combo.png"]
];

const productBenefits = [
  ["100% Sản phẩm chính hãng", "Cam kết chính hãng - Hoàn tiền 200% nếu phát hiện hàng giả.", "shield.png"],
  ["Thành phần an toàn", "Không paraben - Không cồn khô - Không thử nghiệm trên động vật.", "lotus.png"],
  ["Hiệu quả được kiểm chứng", "Được kiểm nghiệm da liễu, hiệu quả rõ rệt sau 7 - 28 ngày.", "vip_shield_round.png"],
  ["Giao hàng toàn quốc", "Giao nhanh 2h nội thành - Miễn phí đơn từ 500.000đ.", "menu_cart.png"],
  ["Tư vấn chuyên sâu", "Đội ngũ chuyên gia luôn sẵn sàng đồng hành cùng làn da bạn.", "expert.png"]
];

const productImageBySlug: Record<string, string> = {
  "perfect-diary": `${assetRoot}/03_san_pham/assets/perfect_cream.png`,
  "son-black-rouge-air-fit": `${assetRoot}/03_san_pham/assets/luxury_lipstick.png`,
  "innisfree-powder": `${assetRoot}/03_san_pham/assets/glow_cushion.png`,
  "the-ordinary-serum": `${assetRoot}/03_san_pham/assets/glow_serum.png`,
  "maybelline-mascara": `${assetRoot}/03_san_pham/assets/glow_cushion.png`,
  bioderma: `${assetRoot}/03_san_pham/assets/premium_toner.png`
};

const homeFeaturedProducts = [
  ["HD Glow Serum", "850.000đ", `${assetRoot}/01_trang_chu/assets/san_pham_glow_serum.png`],
  ["HD Perfect Cream", "950.000đ", `${assetRoot}/01_trang_chu/assets/san_pham_perfect_cream.png`],
  ["HD Premium Toner", "650.000đ", `${assetRoot}/01_trang_chu/assets/san_pham_premium_toner.png`]
];

const combos = [
  ["COMBO GLOW TOÀN DIỆN", "Sáng da - Cấp ẩm - Trẻ hóa", "1.990.000đ", "3.050.000đ", "-35%", `${assetRoot}/04_combo_uu_dai/assets/combo_glow_toan_dien.png`, "BEST SELLER"],
  ["COMBO PHỤC HỒI DA", "Phục hồi - Dưỡng ẩm - Bảo vệ", "2.030.000đ", "2.900.000đ", "-30%", `${assetRoot}/04_combo_uu_dai/assets/combo_phuc_hoi_da.png`, ""],
  ["COMBO CHỐNG LÃO HÓA", "Ngăn ngừa lão hóa - Tái tạo da", "2.090.000đ", "3.200.000đ", "-35%", `${assetRoot}/04_combo_uu_dai/assets/combo_co_dau.png`, ""],
  ["COMBO MAKEUP PARTY", "Tự tin tỏa sáng mọi khoảnh khắc", "1.790.000đ", "2.400.000đ", "-25%", `${assetRoot}/04_combo_uu_dai/assets/combo_makeup_party.png`, ""]
];

const comboBenefits = [
  ["100% Chính hãng", "Cam kết sản phẩm\nchính hãng Hoàn Doãn", "vip_shield_round.png"],
  ["An toàn tuyệt đối", "Không paraben, không cồn khô,\nkhông thử nghiệm trên động vật", "lotus.png"],
  ["Hiệu quả đã kiểm chứng", "Hiệu quả rõ rệt sau 7 - 28 ngày\nsử dụng", "shield.png"],
  ["Giao hàng toàn quốc", "Giao nhanh 2h nội thành,\nmiễn phí đơn từ 500.000đ", "menu_cart.png"],
  ["Tư vấn chuyên sâu", "Đội ngũ chuyên gia da liễu\nđồng hành cùng bạn", "expert.png"]
];

const serviceCategories = [
  ["Tất cả dịch vụ", "vip_shield_round.png"],
  ["Chăm sóc da mặt", "vip_user_round.png"],
  ["Điều trị da chuyên sâu", "calendar_small.png"],
  ["Phun xăm thẩm mỹ", "spray.png"],
  ["Body & Relax", "lotus.png"],
  ["Chăm sóc cơ thể", "heart.png"],
  ["Triệt lông công nghệ cao", "beaker.png"],
  ["Makeup & Styling", "cream.png"],
  ["Đào tạo học viên", "expert.png"]
];

const servicePageCards = [
  ["CHĂM SÓC DA CƠ BẢN", "Làm sạch sâu, dưỡng ẩm và phục hồi làn da khỏe mạnh, tươi sáng.", "60 phút", "350.000đ", `${assetRoot}/02_dich_vu/assets/cham_soc_da.png`, ""],
  ["ĐIỀU TRỊ MỤN CHUYÊN SÂU", "Liệu trình đặc trị mụn, giảm viêm, ngăn ngừa mụn tái phát.", "90 phút", "850.000đ", `${assetRoot}/01_trang_chu/assets/dich_vu_cham_soc_da.png`, "BEST SELLER"],
  ["PHUN MÀY TỰ NHIÊN", "Dáng mày tự nhiên, hài hòa với khuôn mặt.", "120 phút", "2.500.000đ", `${assetRoot}/02_dich_vu/assets/phun_xam_tham_my.png`, ""],
  ["PHUN MÔI COLLAGEN", "Môi căng mọng, tươi tắn với công nghệ hiện đại.", "90 phút", "2.800.000đ", `${assetRoot}/01_trang_chu/assets/dich_vu_phun_xam.png`, ""],
  ["MASSAGE BODY THƯ GIÃN", "Giảm căng thẳng, thư giãn cơ thể, đả thông kinh lạc.", "60 phút", "600.000đ", `${assetRoot}/02_dich_vu/assets/body_relax.png`, ""],
  ["TẮM TRẮNG PHI THUYỀN", "Dưỡng trắng toàn thân, giúp da trắng sáng mịn màng.", "120 phút", "1.600.000đ", `${assetRoot}/01_trang_chu/assets/dich_vu_body_relax.png`, ""],
  ["TRIỆT LÔNG DIODE LASER", "Triệt lông vĩnh viễn, an toàn, không đau rát.", "45 phút", "1.200.000đ", `${assetRoot}/05_dat_lich/assets/hinh_anh_salon.png`, ""],
  ["MAKEUP DỰ TIỆC", "Makeup chuyên nghiệp giúp bạn tự tin tỏa sáng.", "60 phút", "800.000đ", `${assetRoot}/02_dich_vu/assets/makeup_cao_cap.png`, ""]
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
              <Link href="/dang-ky-tu-van" className="home-consult-button">
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
  const [backendProducts, setBackendProducts] = useState<ApiProduct[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [addedProductId, setAddedProductId] = useState("");
  const cartNoticeTimeoutRef = useRef<number | null>(null);

  useEffect(() => {
    fetchProducts()
      .then((items) => {
        setBackendProducts(items);
        setMessage("");
      })
      .catch(() => setMessage("Không tải được sản phẩm từ backend. Vui lòng chạy backend ở cổng 5000."))
      .finally(() => setLoading(false));
  }, []);

  function addProductToCart(product: ApiProduct) {
    const image = productImageBySlug[product.slug] ?? product.imagePath;
    const items = addCartItem(loadCartItems(), { ...product, imagePath: image }, 1);
    saveCartItems(items);
    notifyCartChanged({ productName: product.name });
    setAddedProductId(product.id);
    if (cartNoticeTimeoutRef.current) window.clearTimeout(cartNoticeTimeoutRef.current);
    cartNoticeTimeoutRef.current = window.setTimeout(() => setAddedProductId(""), 2400);
  }

  return (
    <div className="product-page">
      <main className="product-stage mx-auto">
        <Header embedded bare />
        <section className="product-hero-copy">
          <p>Nâng tầm vẻ đẹp Việt</p>
          <h1>Sản phẩm<br />cao cấp</h1>
          <em>Tinh hoa cho làn da rạng rỡ</em>
          <span>Bộ sưu tập mỹ phẩm cao cấp được nghiên cứu chuyên sâu<br />mang đến vẻ đẹp rạng ngời và tự tin mỗi ngày.</span>
        </section>

        <section className="product-shop-panel">
          <nav className="product-tabs">
            {productTabs.map(([label, icon], index) => (
              <Link key={label} href="/shop-my-pham" className={index === 0 ? "active" : ""}>
                <img src={`${homeIconRoot}/${icon}`} alt="" />
                <span>{label}</span>
              </Link>
            ))}
          </nav>

          <div className="product-filters">
            {["Bộ lọc", "Danh mục", "Nhu cầu da", "Thành phần", "Khoảng giá"].map((item, index) => (
              <button key={item} type="button">
                {index === 0 ? <img src={`${homeIconRoot}/vip_dots.png`} alt="" /> : null}
                <span>{item}</span>
                {index > 0 ? <img src={`${homeIconRoot}/chevron_down_1.png`} alt="" /> : null}
              </button>
            ))}
            <label>
              <span>Sắp xếp:</span>
              <select aria-label="Sắp xếp sản phẩm">
                <option>Mới nhất</option>
                <option>Bán chạy</option>
                <option>Giá thấp đến cao</option>
              </select>
            </label>
          </div>

          {message ? <p className="mt-4 rounded-xl border border-[#efbdc5] bg-white/75 p-4 text-center font-bold text-[#d92e55]">{message}</p> : null}
          {loading ? <p className="mt-4 text-center font-bold text-[#70423e]">Đang tải sản phẩm thật từ backend...</p> : null}

          <div className="product-grid">
            {backendProducts.map((product, index) => {
              const image = productImageBySlug[product.slug] ?? product.imagePath;
              const salePrice = product.salePrice ? formatVnd(product.salePrice) : "";
              return (
              <article key={product.id} className="product-shop-card">
                <div className="product-card-top">
                  <span>{product.salePrice ? "SALE" : index === 0 ? "BEST SELLER" : "MỚI"}</span>
                  <button type="button" aria-label={`Yêu thích ${product.name}`}>♡</button>
                </div>
                <img src={image} alt={product.name} />
                <p className="product-rating">★ ★ <span>{index === 3 ? "5.0 (128)" : `4.${9 - (index % 3)} (${256 - index * 33})`}</span></p>
                <h2>{product.name}</h2>
                <p>Còn {product.stock} sản phẩm trong kho</p>
                <p className="product-price"><strong>{formatVnd(product.salePrice ?? product.price)}</strong> <span>{salePrice ? formatVnd(product.price) : ""}</span></p>
                <button onClick={() => addProductToCart(product)} className="relative" type="button">Thêm vào giỏ <img src={`${homeIconRoot}/menu_cart.png`} alt="" />
                  {addedProductId === product.id ? <CartAddedToast productName={product.name} /> : null}
                </button>
              </article>
              );
            })}
          </div>
        </section>

        <footer className="product-benefits">
          {productBenefits.map(([title, text, icon]) => (
            <div key={title}>
              <img src={`${homeIconRoot}/${icon}`} alt="" />
              <section>
                <h3>{title}</h3>
                <p>{text}</p>
              </section>
            </div>
          ))}
        </footer>
      </main>
    </div>
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
    <div className="combo-page">
      <main className="combo-stage mx-auto">
        <Header embedded bare />

        <section className="combo-hero-copy">
          <p>Ưu đãi đặc biệt dành cho bạn</p>
          <h1>Combo ưu đãi<br />cao cấp</h1>
          <em>Đẹp hơn mỗi ngày - Tiết kiệm mỗi khoản</em>
        </section>

        <section className="combo-hero-badges">
          {[
            ["Sản phẩm chính hãng", "100% từ Hoàn Doãn", "shield.png"],
            ["Hiệu quả vượt trội", "Được chuyên gia kiểm chứng", "beaker.png"],
            ["Tiết kiệm lên đến", "40% khi mua combo", "vip_shield_round.png"]
          ].map(([title, text, icon]) => (
            <div key={title}>
              <img src={`${homeIconRoot}/${icon}`} alt="" />
              <span><strong>{title}</strong>{text}</span>
            </div>
          ))}
        </section>

        <section className="combo-offer-panel">
          <h2>Ưu đãi có hạn</h2>
          <p>Chương trình kết thúc sau:</p>
          <div className="combo-countdown">
            {[
              ["02", "Ngày"],
              ["08", "Giờ"],
              ["45", "Phút"],
              ["18", "Giây"]
            ].map(([num, label]) => (
              <div key={label}><strong>{num}</strong><span>{label}</span></div>
            ))}
          </div>
          <div className="combo-gift-line">
            <img src={`${homeIconRoot}/menu_combo.png`} alt="" />
            <span><strong>Quà tặng hấp dẫn</strong>cho đơn combo bất kỳ</span>
          </div>
          <Link href="/combo-uu-dai">Chọn combo ngay <img src={`${homeIconRoot}/arrow_long_1.png`} alt="" /></Link>
          <p className="combo-customer-note">
            <img src={`${homeIconRoot}/menu_combo.png`} alt="" />
            Hơn <strong>12.560</strong> khách hàng<br />đã chọn combo Hoàn Doãn
          </p>
        </section>

        <section className="combo-gifts-panel">
          <h2>Quà tặng đi kèm</h2>
          <p>Cho đơn combo bất kỳ</p>
          <div className="combo-gifts">
            <article><img src={`${assetRoot}/04_combo_uu_dai/assets/hop_qua.png`} alt="" /><span>Mặt nạ HD<br />cao cấp</span></article>
            <article><img src={`${homeIconRoot}/menu_cart.png`} alt="" /><span>Túi mỹ phẩm<br />sang trọng</span></article>
            <article><img src={`${homeIconRoot}/menu_combo.png`} alt="" /><span>Voucher spa<br />500.000đ</span></article>
          </div>
          <strong>Tổng giá trị quà tặng lên đến 1.200.000đ</strong>
          <small>*Số lượng quà tặng có hạn</small>
        </section>

        <section className="combo-card-row">
          {combos.map(([title, desc, price, oldPrice, sale, image, tag]) => (
            <article key={title} className="combo-deal-card">
              {tag ? <span className="combo-card-tag">{tag}</span> : null}
              <h2>{title}</h2>
              <p>{desc}</p>
              <div className="combo-card-image">
                <img src={image} alt={title} />
                <strong>{sale}</strong>
              </div>
              <p className="combo-old-price">Giá gốc: <span>{oldPrice}</span></p>
              <p className="combo-sale-label">Giá ưu đãi</p>
              <p className="combo-new-price">{price}</p>
              <Link href="/gio-hang">Chọn combo <img src={`${homeIconRoot}/arrow_long_1.png`} alt="" /></Link>
            </article>
          ))}
        </section>

        <footer className="combo-benefits">
          {comboBenefits.map(([title, text, icon]) => (
            <div key={title}>
              <img src={`${homeIconRoot}/${icon}`} alt="" />
              <section>
                <h3>{title}</h3>
                <p>{text}</p>
              </section>
            </div>
          ))}
          <p className="combo-brand-strip"><img src={`${homeIconRoot}/vip_crown.png`} alt="" /> Hoàn Doãn Beauty &amp; Academy - Nâng tầm vẻ đẹp Việt</p>
        </footer>
      </main>
    </div>
  );
}

export function ServicesRealPage() {
  return (
    <div className="service-page">
      <main className="service-stage mx-auto">
        <Header embedded bare />
        <section className="service-hero-copy">
          <h1>Dịch vụ làm đẹp</h1>
          <p>Đa dạng dịch vụ chuyên nghiệp, giúp bạn tỏa sáng và tự tin mỗi ngày.</p>
          <span><img src={`${assetRoot}/02_dich_vu/assets/thanh_ngang_dich_vu_clean.png`} alt="" /></span>
        </section>

        <section className="service-content">
          <aside className="service-sidebar">
            <div className="service-category-panel">
              <h2><img src={`${homeIconRoot}/lotus.png`} alt="" />Danh mục dịch vụ</h2>
              <div className="service-category-list">
                {serviceCategories.map(([item, icon], index) => (
                  <Link key={item} href="/dich-vu-makeup" className={index === 0 ? "active" : ""}>
                    <img src={`${homeIconRoot}/${icon}`} alt="" />
                    <span>{item}</span>
                    <img src={`${homeIconRoot}/chevron_right.png`} alt="" />
                  </Link>
                ))}
              </div>
            </div>
            <div className="service-advice">
              <h3>Tư vấn miễn phí</h3>
              <p>Đội ngũ chuyên viên sẵn sàng tư vấn liệu trình phù hợp cho bạn!</p>
              <Link href="/tu-van">Liên hệ ngay <img src={`${homeIconRoot}/phone.png`} alt="" /></Link>
            </div>
          </aside>

          <section className="service-list-panel">
            <div className="service-list-head">
              <h2>Tất cả dịch vụ</h2>
              <label>
                <span>Sắp xếp:</span>
                <select aria-label="Sắp xếp dịch vụ">
                  <option>Phổ biến</option>
                  <option>Giá thấp đến cao</option>
                  <option>Giá cao đến thấp</option>
                </select>
              </label>
            </div>
            <div className="service-card-grid">
              {servicePageCards.map(([title, desc, duration, price, image, tag]) => (
                <article key={title} className="service-card">
                  <div className="service-card-image">
                    {tag ? <span>{tag}</span> : null}
                    <img src={image} alt={title} />
                  </div>
                  <h3>{title}</h3>
                  <p>{desc}</p>
                  <div className="service-meta">
                    <span><img src={`${homeIconRoot}/clock.png`} alt="" />{duration}</span>
                    <strong>{price}</strong>
                  </div>
                  <Link href="/dat-lich">Xem chi tiết <img src={`${homeIconRoot}/arrow_long_2.png`} alt="" /></Link>
                </article>
              ))}
            </div>
          </section>
        </section>
        <TrustStrip framed={false} />
      </main>
    </div>
  );
}

export function BookingRealPage() {
  const [message, setMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [selectedDate, setSelectedDate] = useState("");
  const [selectedTime, setSelectedTime] = useState("");
  const [availableTimes, setAvailableTimes] = useState<string[]>([]);
  const [loadingAvailability, setLoadingAvailability] = useState(false);
  const submissionGateRef = useRef(createBookingSubmissionGate());

  useEffect(() => {
    if (!selectedDate) {
      return;
    }

    let active = true;
    startTransition(() => {
      setLoadingAvailability(true);
      setSelectedTime("");
    });
    fetchAvailableAppointmentSlots(selectedDate).then((result) => {
      if (!active) return;
      if (result.ok) {
        setAvailableTimes(result.slots.map((slot) => slot.time));
      } else {
        setAvailableTimes([]);
        setMessage(result.message);
      }
      setLoadingAvailability(false);
    });
    return () => { active = false; };
  }, [selectedDate]);

  async function submitBooking(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const formData = new FormData(form);
    const date = String(formData.get("date"));
    const time = String(formData.get("time"));

    await submissionGateRef.current.run(async () => {
      setSubmitting(true);
      setMessage("Đang gửi lịch hẹn...");

      try {
        const result = await submitAppointment({
          customerName: formData.get("customerName"),
          phone: formData.get("phone"),
          email: "",
          address: "Hoàn Doãn Beauty & Academy",
          service: formData.get("service"),
          tone: formData.get("specialist"),
          note: formData.get("note"),
          startAt: `${date}T${time}:00+07:00`
        });
        if (!result.ok) {
          setMessage(result.message);
          return;
        }

        setMessage("Đặt lịch thành công! Hoàn Doãn sẽ liên hệ xác nhận với bạn.");
        setAvailableTimes((current) => current.filter((time) => time !== selectedTime));
        setSelectedTime("");
        form.reset();
      } finally {
        setSubmitting(false);
      }
    });
  }

  const steps = [
    ["01", "Chọn dịch vụ", "Lựa chọn dịch vụ bạn mong muốn", "calendar.png"],
    ["02", "Chọn chuyên viên", "Chọn chuyên viên phù hợp", "vip_user_round.png"],
    ["03", "Chọn thời gian", "Chọn ngày và giờ thuận tiện", "calendar_small.png"],
    ["04", "Xác nhận", "Nhận xác nhận qua điện thoại/SMS", "vip_shield_round.png"]
  ];

  const bookingTrust = [
    ["100% Chính hãng", "Cam kết sản phẩm\nchính hãng Hoàn Doãn", "shield.png"],
    ["Đội ngũ chuyên gia", "Chuyên môn cao - Tận tâm\nKinh nghiệm nhiều năm", "expert.png"],
    ["Vệ sinh an toàn tuyệt đối", "Quy trình chuẩn y khoa\nDụng cụ vô trùng", "vip_shield_round.png"],
    ["Không gian sang trọng", "Thư giãn - Riêng tư\nĐẳng cấp & tinh tế", "diamond.png"],
    ["Hàng ngàn khách hàng tin chọn", "Uy tín tạo nên thương hiệu\nNiềm tin tạo nên giá trị", "vip_user_round.png"]
  ];

  return (
    <div className="booking-page">
      <main className="booking-stage mx-auto">
        <Header embedded bare />

        <section className="booking-hero-copy">
          <p><img src={`${homeIconRoot}/vip_crown.png`} alt="" /></p>
          <h1>Đặt lịch</h1>
          <em>Đẹp hơn mỗi ngày<br />Tỏa sáng theo cách riêng</em>
          <span>Hoàn Doãn Beauty &amp; Academy luôn sẵn sàng đồng hành cùng bạn trên hành trình chăm sóc và tôn vinh vẻ đẹp tự nhiên.</span>
        </section>

        <section className="booking-form-card booking-glass">
          <h2>Thông tin đặt lịch</h2>
          <p>Vui lòng điền đầy đủ thông tin để chúng tôi phục vụ bạn tốt nhất</p>
          <form onSubmit={submitBooking}>
            <label className="booking-field">
              <img src={`${homeIconRoot}/lotus.png`} alt="" />
              <span>Dịch vụ<select name="service" required defaultValue=""><option value="" disabled>Chọn dịch vụ</option><option>Chăm sóc da chuyên sâu</option><option>Phun xăm thẩm mỹ</option><option>Makeup cao cấp</option><option>Body & Relax</option></select></span>
              <b>⌄</b>
            </label>
            <label className="booking-field">
              <img src={`${homeIconRoot}/vip_user_round.png`} alt="" />
              <span>Chuyên viên<select name="specialist" required defaultValue=""><option value="" disabled>Chọn chuyên viên</option><option>Chuyên viên cao cấp</option><option>Chuyên gia da liễu</option><option>Chuyên gia makeup</option></select></span>
              <b>⌄</b>
            </label>
            <label className="booking-field">
              <img src={`${homeIconRoot}/calendar.png`} alt="" />
              <span>Ngày hẹn<input name="date" type="date" required value={selectedDate} onChange={(event) => { const date = event.target.value; setSelectedDate(date); if (!date) { setAvailableTimes([]); setSelectedTime(""); } }} /></span>
            </label>
            <label className="booking-field">
              <img src={`${homeIconRoot}/clock.png`} alt="" />
              <span>Giờ hẹn<select name="time" required value={selectedTime} onChange={(event) => setSelectedTime(event.target.value)} disabled={!selectedDate || loadingAvailability}><option value="" disabled>{loadingAvailability ? "Đang tải giờ trống..." : !selectedDate ? "Chọn ngày trước" : availableTimes.length === 0 ? "Không còn giờ trống" : "Chọn giờ"}</option>{availableTimes.map((time) => <option key={time} value={time}>{time}</option>)}</select></span>
              <b>⌄</b>
            </label>
            <label className="booking-field">
              <img src={`${homeIconRoot}/menu_account.png`} alt="" />
              <span>Họ và tên<input name="customerName" required placeholder="Nhập họ và tên của bạn" /></span>
            </label>
            <label className="booking-field">
              <img src={`${homeIconRoot}/phone.png`} alt="" />
              <span>Số điện thoại<input name="phone" required type="tel" placeholder="Nhập số điện thoại liên hệ" /></span>
            </label>
            <label className="booking-field booking-note">
              <img src={`${homeIconRoot}/arrow_long_2.png`} alt="" />
              <span>Ghi chú (không bắt buộc)<input name="note" placeholder="Nhập ghi chú hoặc yêu cầu đặc biệt của bạn..." /></span>
            </label>
            <button type="submit" disabled={submitting}><img src={`${homeIconRoot}/vip_crown.png`} alt="" />{submitting ? "Đang gửi..." : "Xác nhận đặt lịch"}</button>
          </form>
          <div className="booking-security"><img src={`${homeIconRoot}/shield.png`} alt="" />Thông tin của bạn được bảo mật tuyệt đối và chỉ dùng cho mục đích đặt lịch.</div>
          {message ? <p className="mt-4 rounded bg-white/70 p-3 text-[14px] font-bold text-[#9d3c35]">{message}</p> : null}
          <small><img src={`${homeIconRoot}/vip_shield_round.png`} alt="" />Chúng tôi sẽ liên hệ xác nhận lịch hẹn trong thời gian sớm nhất</small>
        </section>

        <aside className="booking-process booking-glass">
          <h2>Quy trình đặt lịch</h2>
          <div>
            {steps.map(([num, title, text, icon]) => (
              <article key={num}>
                <img src={`${homeIconRoot}/${icon}`} alt="" />
                <strong>{num}</strong>
                <section>
                  <h3>{title}</h3>
                  <p>{text}</p>
                </section>
              </article>
            ))}
          </div>
        </aside>

        <aside className="booking-hotline booking-glass">
          <img src={`${homeIconRoot}/phone.png`} alt="" />
          <section>
            <h2>Hỗ trợ nhanh</h2>
            <strong>1900 1234</strong>
            <p>Hotline hoạt động 8:00 - 21:00<br />(Tất cả các ngày trong tuần)</p>
          </section>
        </aside>

        <aside className="booking-vip booking-glass">
          <img src={`${assetRoot}/04_combo_uu_dai/assets/hop_qua.png`} alt="" />
          <section>
            <h3>Ưu tiên khách hàng VIP</h3>
            <p>Đặt lịch nhanh chóng<br />Nhận ưu đãi đặc quyền</p>
          </section>
        </aside>

        <footer className="booking-trust booking-glass">
          {bookingTrust.map(([title, text, icon]) => (
            <div key={title}>
              <img src={`${homeIconRoot}/${icon}`} alt="" />
              <section>
                <h3>{title}</h3>
                <p>{text}</p>
              </section>
            </div>
          ))}
          <p>Hoàn Doãn Beauty &amp; Academy - Nâng tầm vẻ đẹp Việt</p>
        </footer>
      </main>
    </div>
  );
}

export function ConsultRealPage() {
  const contactItems = [
    { title: "Địa chỉ", text: "123 Đường Hoa Hồng, Phường 2,\nQuận Phú Nhuận, TP. Hồ Chí Minh", icon: "menu_booking.png" },
    { title: "Điện thoại", text: "1900 1234", icon: "phone.png" },
    { title: "Email", text: "info@hoandoanbeauty.vn", icon: "menu_cart.png" },
    { title: "Giờ làm việc", text: "08:00 - 20:00\n(Tất cả các ngày trong tuần)", icon: "clock.png" }
  ];

  const formRows = [
    { placeholder: "Họ và tên", icon: "menu_account.png", selectable: false },
    { placeholder: "Số điện thoại", icon: "phone.png", selectable: false },
    { placeholder: "Email", icon: "menu_cart.png", selectable: false },
    { placeholder: "Chủ đề liên hệ", icon: "menu_service.png", selectable: true },
    { placeholder: "Nội dung tin nhắn", icon: "arrow_long_2.png", selectable: false }
  ];

  const trustRows = [
    { title: "Hỗ trợ nhanh chóng", text: "Phản hồi trong 30 phút\nqua nhiều kênh liên hệ", icon: "phone.png" },
    { title: "Tư vấn chuyên sâu", text: "Đội ngũ chuyên viên\ngiàu kinh nghiệm", icon: "vip_user_round.png" },
    { title: "Không gian sang trọng", text: "Trải nghiệm dịch vụ đẳng cấp,\ntiện nghi hiện đại", icon: "diamond.png" },
    { title: "Sản phẩm chính hãng", text: "Cam kết 100% chính hãng,\nnguồn gốc rõ ràng", icon: "shield.png" },
    { title: "Chăm sóc tận tâm", text: "Đồng hành cùng bạn trên\nhành trình làm đẹp", icon: "heart.png" }
  ];

  return (
    <div className="consult-page">
      <main className="consult-stage mx-auto">
        <Header embedded bare />

        <section className="consult-hero-copy">
          <p><img src={`${homeIconRoot}/vip_crown.png`} alt="" /></p>
          <h1>Liên hệ</h1>
          <em>Chúng tôi luôn sẵn sàng<br />lắng nghe và hỗ trợ bạn</em>
        </section>

        <aside className="consult-contact consult-glass">
          <h2>Thông tin liên hệ</h2>
          <div>
            {contactItems.map(({ title, text, icon }) => (
              <article key={title}>
                <img src={`${homeIconRoot}/${icon}`} alt="" />
                <section>
                  <h3>{title}</h3>
                  <p>{text}</p>
                </section>
              </article>
            ))}
          </div>
        </aside>

        <aside className="consult-vip consult-glass">
          <img src={`${assetRoot}/04_combo_uu_dai/assets/hop_qua.png`} alt="" />
          <section>
            <h3>Hỗ trợ khách hàng VIP</h3>
            <p>Ưu tiên phản hồi trong<br />30 phút dành riêng cho bạn</p>
            <Link href="/ho-tro-vip">Đăng ký ngay <img src={`${homeIconRoot}/chevron_right.png`} alt="" /></Link>
          </section>
        </aside>

        <section className="consult-form consult-glass">
          <h2>Gửi tin nhắn cho chúng tôi</h2>
          <p>Vui lòng điền đầy đủ thông tin. Đội ngũ Hoàn Doãn sẽ phản hồi cho bạn trong thời gian sớm nhất.</p>
          <form>
            {formRows.map(({ placeholder, icon, selectable }, index) => (
              <label key={placeholder} className={index === formRows.length - 1 ? "consult-input consult-message" : "consult-input"}>
                <img src={`${homeIconRoot}/${icon}`} alt="" />
                <input placeholder={placeholder} readOnly />
                {selectable ? <b>⌄</b> : null}
              </label>
            ))}
          </form>
          <Link className="consult-submit-link" href="/dang-ky-tu-van"><img src={`${homeIconRoot}/arrow_long_1.png`} alt="" />Đăng ký lịch tư vấn</Link>
          <small><img src={`${homeIconRoot}/shield.png`} alt="" />Thông tin của bạn được bảo mật tuyệt đối</small>
        </section>

        <aside className="consult-map consult-glass">
          <h2>Tìm đường đến chúng tôi</h2>
          <div className="consult-map-art">
            <span>HD<br /><small>Hoàn Doãn</small></span>
          </div>
          <Link href="/dat-lich">Xem chỉ đường <img src={`${homeIconRoot}/arrow_long_1.png`} alt="" /></Link>
        </aside>

        <footer className="consult-trust consult-glass">
          {trustRows.map(({ title, text, icon }) => (
            <div key={title}>
              <img src={`${homeIconRoot}/${icon}`} alt="" />
              <section>
                <h3>{title}</h3>
                <p>{text}</p>
              </section>
            </div>
          ))}
        </footer>
      </main>
    </div>
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
  const accountMenu = [
    ["Tổng quan", "menu_home.png"],
    ["Đơn hàng của tôi", "menu_cart.png"],
    ["Lịch hẹn", "calendar_small.png"],
    ["Sản phẩm yêu thích", "heart.png"],
    ["Địa chỉ", "vip_shield_round.png"],
    ["Thông tin cá nhân", "menu_account.png"],
    ["Thành viên VIP", "vip_crown.png"],
    ["Điểm thưởng", "diamond.png"],
    ["Phương thức thanh toán", "menu_cart.png"],
    ["Bảo mật tài khoản", "shield.png"],
    ["Đăng xuất", "arrow_long_2.png"]
  ];

  const stats = [
    ["Tổng đơn hàng", "18", "Đơn hàng", "menu_cart.png"],
    ["Tổng chi tiêu", "18.650.000đ", "VND", "clock.png"],
    ["Lịch hẹn sắp tới", "03", "Lịch hẹn", "calendar.png"],
    ["Sản phẩm yêu thích", "12", "Sản phẩm", "heart.png"],
    ["Điểm thưởng khả dụng", "2.450", "Điểm", "diamond.png"]
  ];

  const appointments = [
    ["Chăm sóc da chuyên sâu", "Với chuyên viên Lan Anh", "23/05/2025 - 10:00 AM", "lich_hen_01_cham_soc_da.png"],
    ["Phun xăm thẩm mỹ", "Với chuyên viên Thu Hà", "27/05/2025 - 14:00 PM", "lich_hen_02_phun_xam.png"],
    ["Makeup cá nhân", "Với chuyên viên Mai Phương", "31/05/2025 - 09:00 AM", "lich_hen_03_makeup.png"]
  ];

  const favorites = [
    ["HD Perfect Cream", "Kem dưỡng cao cấp", "950.000đ", "yeu_thich_01_perfect_cream.png"],
    ["HD Glow Serum", "Tinh chất dưỡng sáng", "850.000đ", "yeu_thich_02_glow_serum.png"],
    ["HD Premium Toner", "Nước hoa hồng cao cấp", "650.000đ", "yeu_thich_03_premium_toner.png"]
  ];

  const orders = [
    ["#HD12560", "31/05/2025", "Đã giao hàng", "2.850.000đ"],
    ["#HD12480", "10/05/2025", "Đã giao hàng", "1.950.000đ"],
    ["#HD12420", "02/05/2025", "Đã giao hàng", "950.000đ"],
    ["#HD12360", "15/04/2025", "Đã giao hàng", "3.450.000đ"]
  ];

  const info = [
    ["Họ và tên", "Nguyễn Thảo My", "menu_account.png"],
    ["Số điện thoại", "0901 234 567", "phone.png"],
    ["Email", "thaomy@gmail.com", "menu_booking.png"],
    ["Ngày sinh", "15/06/1995", "calendar_small.png"],
    ["Giới tính", "Nữ", "account_round.png"],
    ["Địa chỉ", "123 Đường Hoa Lan, P.2,\nQ. Phú Nhuận, TP. Hồ Chí Minh", "vip_shield_round.png"]
  ];

  return (
    <div className="account-page">
      <main className="account-stage mx-auto">
        <Header embedded bare />

        <aside className="account-sidebar account-glass">
          <nav>
            {accountMenu.map(([label, icon], index) => (
              <Link key={label} href="/tai-khoan" className={index === 0 ? "active" : ""}>
                <img src={`${homeIconRoot}/${icon}`} alt="" />
                <span>{label}</span>
              </Link>
            ))}
          </nav>
        </aside>

        <section className="account-support account-glass">
          <h2>Cần hỗ trợ?</h2>
          <p>Đội ngũ Hoàn Doãn luôn sẵn sàng đồng hành cùng bạn</p>
          <Link href="/tu-van"><img src={`${homeIconRoot}/phone.png`} alt="" />Liên hệ ngay</Link>
        </section>

        <section className="account-profile account-glass">
          <img className="account-avatar" src={`${assetRoot}/06_tai_khoan/assets/avatar_tai_khoan.png`} alt="Nguyễn Thảo My" />
          <div className="account-profile-copy">
            <p>Xin chào,</p>
            <h1>Nguyễn Thảo My <span>✎</span></h1>
            <strong><img src={`${homeIconRoot}/vip_crown.png`} alt="" />Thành viên VIP PRO MAX</strong>
            <div>
              <span>Điểm thưởng <b>2.450</b> Điểm</span>
              <span>Hạng của bạn <b>VIP PRO MAX</b></span>
            </div>
          </div>
        </section>

        <section className="account-vip account-glass">
          <div>
            <p>Thẻ thành viên</p>
            <h2>VIP PRO MAX</h2>
            <span>Hiệu lực đến: <b>31/12/2025</b></span>
            <Link href="/tai-khoan">Xem quyền lợi <img src={`${homeIconRoot}/arrow_long_2.png`} alt="" /></Link>
          </div>
          <div className="account-vip-card">HD<br /><span>VIP PRO MAX</span></div>
        </section>

        <section className="account-stats">
          {stats.map(([label, value, unit, icon]) => (
            <article key={label} className="account-glass">
              <img src={`${homeIconRoot}/${icon}`} alt="" />
              <span>{label}</span>
              <strong>{value}</strong>
              <small>{unit}</small>
            </article>
          ))}
        </section>

        <section className="account-panel account-appointments account-glass">
          <header><h2>Lịch hẹn sắp tới</h2><Link href="/dat-lich">Xem tất cả</Link></header>
          {appointments.map(([title, person, time, image]) => (
            <article key={title}>
              <img src={`${assetRoot}/06_tai_khoan/assets/${image}`} alt={title} />
              <div><h3>{title}</h3><p>{person}</p><span>{time}</span></div>
              <b>Sắp tới</b>
            </article>
          ))}
          <Link className="account-panel-action" href="/dat-lich">Xem tất cả lịch hẹn <img src={`${homeIconRoot}/arrow_long_2.png`} alt="" /></Link>
        </section>

        <section className="account-panel account-favorites account-glass">
          <header><h2>Sản phẩm yêu thích</h2><Link href="/shop-my-pham">Xem tất cả</Link></header>
          {favorites.map(([title, desc, price, image]) => (
            <article key={title}>
              <img src={`${assetRoot}/06_tai_khoan/assets/${image}`} alt={title} />
              <div><h3>{title}</h3><p>{desc}</p><strong>{price}</strong></div>
              <span>♡</span>
            </article>
          ))}
          <Link className="account-panel-action" href="/shop-my-pham">Xem tất cả yêu thích <img src={`${homeIconRoot}/arrow_long_2.png`} alt="" /></Link>
        </section>

        <section className="account-panel account-orders account-glass">
          <header><h2>Đơn hàng gần đây</h2><Link href="/gio-hang">Xem tất cả</Link></header>
          {orders.map(([code, date, status, total]) => (
            <article key={code}>
              <img src={`${homeIconRoot}/menu_cart.png`} alt="" />
              <div><h3>{code}</h3><p>{date}</p></div>
              <b>{status}</b>
              <strong>{total}</strong>
              <span>›</span>
            </article>
          ))}
          <Link className="account-panel-action" href="/gio-hang">Xem tất cả đơn hàng <img src={`${homeIconRoot}/arrow_long_2.png`} alt="" /></Link>
        </section>

        <footer className="account-info account-glass">
          <h2>Thông tin cá nhân</h2>
          <div>
            {info.map(([label, value, icon]) => (
              <article key={label}>
                <img src={`${homeIconRoot}/${icon}`} alt="" />
                <div>
                  <span>{label}</span>
                  <strong>{value}</strong>
                </div>
              </article>
            ))}
          </div>
          <Link href="/tai-khoan"><span>✎</span>Chỉnh sửa</Link>
        </footer>
      </main>
    </div>
  );
}

export function CartRealPage() {
  const [cartItems, setCartItems] = useState<CartItem[]>([]);
  const [message, setMessage] = useState("");
  const summary = getCartSummary(cartItems);
  const suggestedItems = [
    ["BEST SELLER", "HD REPAIR ESSENCE", "Tinh chất phục hồi chuyên sâu", "750.000đ", `${assetRoot}/07_gio_hang/assets/glow_serum_goi_y.png`],
    ["-15%", "HD LUXURY LIPSTICK", "Son dưỡng cao cấp", "450.000đ", `${assetRoot}/07_gio_hang/assets/lip_treatment_goi_y.png`],
    ["", "HD GLOW CUSHION", "Phấn nước căng bóng SPF50+", "650.000đ", `${assetRoot}/07_gio_hang/assets/perfect_cream_goi_y.png`],
    ["MỚI", "HD CLEANSER", "Sữa rửa mặt dịu nhẹ", "320.000đ", `${assetRoot}/07_gio_hang/assets/sunscreen_spf50_goi_y.png`]
  ];

  useEffect(() => {
    const storedItems = loadCartItems();
    startTransition(() => setCartItems(storedItems));

    fetchProducts()
      .then((products) => {
        const displayProducts = products.map((product) => ({
          ...product,
          imagePath: productImageBySlug[product.slug] ?? product.imagePath
        }));
        const reconciled = reconcileCartItems(storedItems, displayProducts);
        setCartItems(reconciled);
        saveCartItems(reconciled);
        notifyCartChanged();
        setMessage("");
      })
      .catch(() => setMessage("Không tải được sản phẩm mới nhất từ backend. Giỏ hàng đang hiển thị dữ liệu đã lưu."));
  }, []);

  function updateCart(nextItems: CartItem[]) {
    setCartItems(nextItems);
    saveCartItems(nextItems);
    notifyCartChanged();
  }

  return (
    <div className="cart-page">
      <main className="cart-stage mx-auto">
        <Header embedded bare />

        <section className="cart-hero-copy">
          <p>Nâng tầm vẻ đẹp Việt</p>
          <h1>Giỏ hàng</h1>
          <span>Kiểm tra sản phẩm và hoàn tất đơn hàng của bạn</span>
        </section>

        <section className="cart-grid">
          <section className="cart-table cart-glass">
            <header>
              <span></span>
              <span>Ảnh</span>
              <span>Tên sản phẩm</span>
              <span>Đơn giá</span>
              <span>Số lượng</span>
              <span>Thành tiền</span>
              <span>Thao tác</span>
            </header>
            {cartItems.length === 0 ? (
              <article>
                <div></div>
                <div></div>
                <div>
                  <h2>Giỏ hàng đang trống</h2>
                  <p>Hãy thêm sản phẩm thật từ trang shop mỹ phẩm.</p>
                  <small>Dữ liệu giỏ hàng sẽ được lưu trên trình duyệt của bạn.</small>
                </div>
                <strong>-</strong>
                <div className="cart-qty"><span>0</span></div>
                <b>-</b>
                <Link href="/shop-my-pham" className="cart-remove" aria-label="Tiếp tục mua sắm">+</Link>
              </article>
            ) : cartItems.map((item) => (
              <article key={item.id}>
                <label aria-label={`Chọn ${item.name}`}><input type="checkbox" defaultChecked /></label>
                <img src={item.imagePath} alt={item.name} />
                <div>
                  <h2>{item.name}</h2>
                  <p>{item.slug}</p>
                  <small>Còn {item.stock} sản phẩm trong kho</small>
                </div>
                <strong>{formatVnd(getCartUnitPrice(item))}</strong>
                <div className="cart-qty">
                  <button onClick={() => updateCart(decreaseCartItem(cartItems, item.id))} type="button" aria-label={`Giảm ${item.name}`}>-</button>
                  <span>{item.quantity}</span>
                  <button onClick={() => updateCart(increaseCartItem(cartItems, item.id))} type="button" aria-label={`Tăng ${item.name}`}>+</button>
                </div>
                <b>{formatVnd(getCartUnitPrice(item) * item.quantity)}</b>
                <button onClick={() => updateCart(removeCartItem(cartItems, item.id))} className="cart-remove" type="button" aria-label={`Xóa ${item.name}`}>⌫</button>
              </article>
            ))}
            <footer>
              <label><input type="checkbox" defaultChecked /> Chọn tất cả ({summary.totalQuantity})</label>
              <button onClick={() => updateCart(clearCartItems())} type="button">⌫ Xóa tất cả</button>
              <Link href="/shop-my-pham">← Tiếp tục mua sắm</Link>
            </footer>
          </section>

          <aside className="cart-summary cart-glass">
            <h2>Tóm tắt đơn hàng</h2>
            <p><span>Tổng số lượng</span><strong>{summary.totalQuantity}</strong></p>
            <p><span>Tạm tính</span><strong>{formatVnd(summary.totalPrice)}</strong></p>
            <p><span>Giảm giá</span><strong>-</strong></p>
            <label>
              <span>Mã giảm giá</span>
              <div><input placeholder="Chưa áp dụng ở ticket này" /><button type="button">Áp dụng</button></div>
              <small>Checkout và mã giảm giá sẽ được xử lý ở ticket sau.</small>
            </label>
            <div className="cart-total">
              <span>Tổng cộng<small>(Đã bao gồm VAT)</small></span>
              <strong>{formatVnd(summary.totalPrice)}</strong>
            </div>
            <Link href="/thanh-toan" aria-disabled={cartItems.length === 0}>Tiến hành thanh toán <span>→</span></Link>
            {message ? <p className="mt-3 rounded bg-white/70 p-3 text-[13px] font-bold text-[#9d3c35]">{message}</p> : null}
            <div className="cart-payments">
              <p>Phương thức thanh toán</p>
              <span>VISA</span><span>●●</span><span>MoMo</span><span>ZaloPay</span>
            </div>
          </aside>
        </section>

        <section className="cart-lower-grid">
          <section className="cart-suggestions cart-glass">
            <h2>Gợi ý cho bạn</h2>
            <div>
              {suggestedItems.map(([tag, title, desc, price, image]) => (
                <article key={title}>
                  {tag ? <span>{tag}</span> : null}
                  <img src={image} alt={title} />
                  <h3>{title}</h3>
                  <p>{desc}</p>
                  <div className="cart-suggest-bottom">
                    <strong>{price}</strong>
                    <button type="button" aria-label={`Thêm ${title} vào giỏ`}>🛒</button>
                  </div>
                </article>
              ))}
            </div>
          </section>

          <section className="cart-service cart-glass">
            <h2>Cam kết dịch vụ</h2>
            <div>
              {productBenefits.slice(0, 4).map(([title, text, icon]) => (
                <article key={title}>
                  <img src={`${homeIconRoot}/${icon}`} alt="" />
                  <section>
                    <h3>{title}</h3>
                    <p>{text}</p>
                  </section>
                </article>
              ))}
            </div>
          </section>
        </section>

        <footer className="cart-trust cart-glass">
          {trustItems.map(([title, text, icon]) => (
            <div key={title}>
              <img src={`${homeIconRoot}/${icon}`} alt="" />
              <section>
                <h3>{title}</h3>
                <p>{text}</p>
              </section>
            </div>
          ))}
        </footer>
      </main>
    </div>
  );
}
