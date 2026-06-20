import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { iconBase } from "@/lib/data";

const accountTabs = [
  { label: "Thông tin tài khoản", icon: `${iconBase}/04_icon_tai_khoan_lien_he/15_nguoi_dung.png`, active: true },
  { label: "Đơn hàng của tôi", icon: `${iconBase}/04_icon_tai_khoan_lien_he/16_tui_mua_sam.png` },
  { label: "Lịch hẹn của tôi", icon: `${iconBase}/04_icon_tai_khoan_lien_he/02_lich_hen.png` },
  { label: "Sản phẩm yêu thích", icon: `${iconBase}/04_icon_tai_khoan_lien_he/17_trai_tim.png` },
  { label: "Địa chỉ của tôi", icon: `${iconBase}/04_icon_tai_khoan_lien_he/03_vi_tri.png` },
  { label: "Đổi mật khẩu", icon: `${iconBase}/04_icon_tai_khoan_lien_he/08_khoa.png` },
  { label: "Đăng xuất", icon: `${iconBase}/04_icon_tai_khoan_lien_he/09_dang_xuat.png` }
];

function Field({ label, value, wide = false, required = false }: { label: string; value: string; wide?: boolean; required?: boolean }) {
  return (
    <label className={wide ? "col-span-2 max-[760px]:col-span-1" : ""}>
      <span className="text-[13px] font-semibold text-brand-ink">{label} {required ? <span className="text-brand-red">*</span> : null}</span>
      <input className="mt-2 h-11 w-full rounded-md border border-[#f3c9d3] bg-white px-4 text-[14px] outline-none focus:border-brand-red" defaultValue={value} />
    </label>
  );
}

export default function AccountPage() {
  return (
    <>
      <Header />
      <main className="bg-white">
        <section className="w-full px-4 pt-4">
          <div className="mx-auto w-full max-w-[1600px] overflow-hidden rounded-[32px] shadow-[0_14px_34px_rgba(217,46,85,0.08)]">
            <img src="/images/products/6_banner_net_hon/05_banner_tai_khoan.png" alt="Tài khoản của tôi" className="block h-auto w-full" />
          </div>
        </section>

        <section className="container-beauty grid grid-cols-[290px_1fr] gap-4 py-6 max-[980px]:grid-cols-1">
          <aside className="overflow-hidden rounded-[8px] border border-[#f3c9d3] bg-white">
            {accountTabs.map((tab) => (
              <button key={tab.label} className={`flex min-h-[78px] w-full items-center gap-4 border-b border-[#f3dbe1] px-7 text-left text-[16px] font-semibold last:border-b-0 ${tab.active ? "bg-brand-pale text-brand-red" : "text-brand-ink"}`} type="button">
                <img src={tab.icon} alt="" className={`h-7 w-7 object-contain ${tab.active ? "" : "grayscale"}`} />
                {tab.label}
              </button>
            ))}
          </aside>

          <section className="rounded-[8px] border border-[#f3c9d3] bg-white px-7 py-6 max-[640px]:px-4">
            <div className="flex items-start gap-3">
              <img src={`${iconBase}/04_icon_tai_khoan_lien_he/15_nguoi_dung.png`} alt="" className="mt-1 h-7 w-7 object-contain" />
              <div>
                <h2 className="font-serif text-[24px] font-bold uppercase text-brand-red">Thông tin tài khoản</h2>
                <p className="mt-1 text-[13px] text-brand-muted">Cập nhật thông tin cá nhân để bảo mật tài khoản và nhận ưu đãi tốt hơn.</p>
              </div>
            </div>

            <div className="mt-7 grid grid-cols-[170px_1fr] items-center gap-8 border-b border-brand-line pb-8 max-[640px]:grid-cols-1">
              <div className="relative h-[150px] w-[150px] justify-self-center rounded-full border-4 border-white bg-brand-pale shadow-[0_0_0_1px_#f3c9d3]">
                <img src="/images/products/hoan_doan_mau_make_san_pham_va_banner/01_mau_make_ban_nu/11_tai_khoan_avatar_nguoi_dung.png" alt="Nguyễn Hoài An" className="h-full w-full rounded-full object-cover" />
                <span className="absolute bottom-3 right-0 grid h-10 w-10 place-items-center rounded-full bg-brand-red">
                  <img src={`${iconBase}/04_icon_tai_khoan_lien_he/07_camera.png`} alt="" className="h-6 w-6 object-contain brightness-0 invert" />
                </span>
              </div>
              <div className="grid grid-cols-2 gap-x-14 gap-y-5 text-[14px] max-[640px]:grid-cols-1">
                <p><span className="block text-brand-muted">Họ và tên</span><strong className="mt-1 block font-medium">Nguyễn Hoài An</strong></p>
                <p><span className="block text-brand-muted">Email</span><strong className="mt-1 block font-medium">hoaian.nguyen@gmail.com</strong></p>
                <p><span className="block text-brand-muted">Số điện thoại</span><strong className="mt-1 block font-medium">0987 654 321</strong></p>
              </div>
            </div>

            <form className="mt-6 grid grid-cols-2 gap-x-8 gap-y-5 max-[760px]:grid-cols-1">
              <Field label="Họ và tên" value="Nguyễn Hoài An" required />
              <Field label="Ngày sinh" value="12/05/1995" />
              <Field label="Email" value="hoaian.nguyen@gmail.com" required />
              <Field label="Số điện thoại" value="0987 654 321" required />

              <div>
                <p className="text-[13px] font-semibold text-brand-ink">Giới tính</p>
                <div className="mt-4 flex gap-10 text-[14px]">
                  {["Nữ", "Nam", "Khác"].map((item, index) => (
                    <label key={item} className="flex items-center gap-2">
                      <span className={`grid h-4 w-4 place-items-center rounded-full border ${index === 0 ? "border-brand-red" : "border-[#f3c9d3]"}`}>{index === 0 ? <span className="h-2 w-2 rounded-full bg-brand-red" /> : null}</span>
                      {item}
                    </label>
                  ))}
                </div>
              </div>
              <Field label="Địa chỉ" value="123 Đường Hoa Lan, Phường 2, Quận Phú Nhuận, TP. HCM" />
              <Field label="Ghi chú" value="Nhập ghi chú của bạn (nếu có)" wide />

              <div className="col-span-2 mt-1 flex justify-end max-[760px]:col-span-1">
                <button className="btn-red min-h-[44px] rounded-md px-8" type="button">
                  <span className="mr-2">▣</span> Lưu thông tin
                </button>
              </div>
            </form>
          </section>
        </section>
      </main>
      <Footer />
    </>
  );
}
