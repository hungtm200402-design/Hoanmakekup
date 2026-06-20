import Link from "next/link";
import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";

export default function CartPage() {
  return (
    <>
      <Header />
      <main className="pb-12">
        <section className="w-full px-4 pt-4">
          <div className="mx-auto w-full max-w-[1600px] overflow-hidden rounded-[32px] shadow-[0_14px_34px_rgba(217,46,85,0.08)]">
            <img src="/images/products/6_banner_net_hon/06_banner_gio_hang.png" alt="Giỏ hàng" className="block h-auto w-full" />
          </div>
        </section>

        <section className="container-beauty pt-12">
        <div className="mt-9 grid grid-cols-[1fr_360px] gap-8 max-[900px]:grid-cols-1">
          <article className="grid grid-cols-[150px_1fr] gap-6 rounded border border-brand-line p-5 max-[520px]:grid-cols-1">
            <img className="h-[150px] w-[150px] rounded bg-brand-soft object-contain p-3" src="/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/18_gio_hang_son_li_peach_coral.png" alt="Son Black Rouge Air Fit" />
            <div>
              <h2 className="text-[22px] font-bold">Son Black Rouge Air Fit</h2>
              <p className="mt-3 text-brand-muted">Màu sắc: A12 - Đỏ Cam</p>
              <p className="mt-3 text-brand-muted">Số lượng: 1</p>
              <p className="mt-5 text-[20px] font-bold text-brand-red">250.000đ</p>
            </div>
          </article>
          <aside className="rounded border border-brand-line p-6">
            <h2 className="text-[20px] font-bold">Tóm tắt đơn hàng</h2>
            <div className="mt-5 grid gap-4 text-[14px]">
              <p className="flex justify-between"><span>Tạm tính</span><strong>250.000đ</strong></p>
              <p className="flex justify-between"><span>Vận chuyển</span><span>Miễn phí</span></p>
              <p className="flex justify-between border-t border-brand-line pt-4 text-[18px]"><span>Tổng cộng</span><strong className="text-brand-red">250.000đ</strong></p>
            </div>
            <Link href="/thanh-toan" className="btn-red mt-7 w-full">Thanh toán</Link>
          </aside>
        </div>
        </section>
      </main>
      <Footer />
    </>
  );
}
