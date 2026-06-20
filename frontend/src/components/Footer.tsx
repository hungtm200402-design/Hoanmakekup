import { iconBase } from "@/lib/data";

const benefits = [
  { title: "Miễn phí giao hàng", text: "Cho đơn hàng từ 499k", icon: `${iconBase}/02_icon_dich_vu_uy_tin/13_giao_hang.png` },
  { title: "Đổi trả dễ dàng", text: "Trong vòng 7 ngày", icon: `${iconBase}/03_icon_thao_tac_san_pham/05_lam_moi.png` },
  { title: "Sản phẩm chính hãng", text: "Cam kết 100% chính hãng", icon: `${iconBase}/02_icon_dich_vu_uy_tin/06_khien_an_toan.png` },
  { title: "Tư vấn nhiệt tình", text: "Hỗ trợ 24/7", icon: `${iconBase}/02_icon_dich_vu_uy_tin/14_tu_van.png` }
];

const socials = [
  `${iconBase}/04_icon_tai_khoan_lien_he/11_facebook.png`,
  `${iconBase}/04_icon_tai_khoan_lien_he/12_instagram.png`,
  `${iconBase}/04_icon_tai_khoan_lien_he/13_tiktok.png`,
  `${iconBase}/04_icon_tai_khoan_lien_he/14_youtube.png`
];

export function Footer() {
  return (
    <footer className="bg-white pb-8">
      <div className="container-beauty rounded-[10px] border border-[#f5cfd8] bg-white px-8 py-7 max-[560px]:px-4">
        <div className="grid grid-cols-4 gap-0 border-b border-brand-line pb-7 max-[900px]:grid-cols-2 max-[560px]:grid-cols-1">
          {benefits.map((item) => (
            <div key={item.title} className="flex items-center justify-center gap-5 border-r border-brand-line px-6 last:border-r-0 max-[560px]:justify-start max-[560px]:border-b max-[560px]:border-r-0 max-[560px]:py-4">
              <img src={item.icon} alt="" className="h-12 w-12 object-contain" />
              <div>
                <p className="text-[15px] font-extrabold text-brand-red">{item.title}</p>
                <p className="mt-1 text-[13px] text-brand-muted">{item.text}</p>
              </div>
            </div>
          ))}
        </div>

        <div className="grid grid-cols-[1.15fr_0.8fr_0.9fr_1.2fr] gap-10 py-12 max-[980px]:grid-cols-2 max-[560px]:grid-cols-1">
          <div>
            <img src="/images/logo-menu-transparent.png" alt="Hoàn Doãn Beauty & Academy" className="h-[110px] w-[260px] object-contain object-left" />
            <p className="mt-5 max-w-[300px] text-[14px] leading-7 text-brand-muted">Hoàn Doãn Beauty & Academy mang đến những dịch vụ và sản phẩm làm đẹp chất lượng cao, giúp bạn tỏa sáng vẻ đẹp tự tin và rạng rỡ mỗi ngày.</p>
          </div>
          <div>
            <h3 className="text-[16px] font-extrabold uppercase text-brand-red">Về chúng tôi</h3>
            {["Giới thiệu", "Dịch vụ", "Sản phẩm", "Tin tức"].map((item) => <p key={item} className="border-b border-brand-line py-4 text-[14px]">{item} <span className="float-right">›</span></p>)}
          </div>
          <div>
            <h3 className="text-[16px] font-extrabold uppercase text-brand-red">Chính sách</h3>
            {["Chính sách bảo mật", "Chính sách đổi trả", "Chính sách vận chuyển", "Điều khoản sử dụng"].map((item) => <p key={item} className="border-b border-brand-line py-4 text-[14px]">{item}</p>)}
          </div>
          <div>
            <h3 className="text-[16px] font-extrabold uppercase text-brand-red">Liên hệ</h3>
            <div className="mt-5 grid gap-4 text-[14px] leading-6">
              <p>123 Đường Hoa Hồng, P.2, Q. Phú Nhuận, TP. Hồ Chí Minh</p>
              <p>0901 234 567</p>
              <p>info@hoandoanbeauty.vn</p>
              <p>www.hoandoanbeauty.vn</p>
            </div>
            <div className="mt-6 flex gap-3">
              {socials.map((icon) => <img key={icon} src={icon} alt="" className="h-9 w-9 object-contain" />)}
            </div>
          </div>
        </div>

        <p className="border-t border-brand-line pt-6 text-center text-[13px] text-brand-muted">© 2024 Hoàn Doãn Beauty & Academy. All rights reserved.</p>
      </div>
    </footer>
  );
}
