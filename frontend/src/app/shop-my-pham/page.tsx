import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { ProductGrid } from "@/components/ProductGrid";
import { iconBase } from "@/lib/data";

const categories = [
  ["Tất cả sản phẩm", "02_them.png"],
  ["Trang điểm", "13_co_trang_diem.png"],
  ["Chăm sóc da", "09_chai_my_pham.png"],
  ["Chăm sóc môi", "11_moi.png"],
  ["Chăm sóc mắt", "12_mat.png"],
  ["Dụng cụ làm đẹp", "15_lua_chon.png"],
  ["Nước hoa", "14_nuoc_hoa.png"]
];

const brands = ["Tất cả thương hiệu", "Maybelline", "L'Oréal", "MAC", "Laneige", "The Ordinary", "3CE", "Innisfree"];

export default function ShopPage() {
  return (
    <>
      <Header />
      <main className="container-beauty pb-12">
        <section className="relative mb-7 overflow-hidden rounded-[10px] border border-[#f7d8df] bg-gradient-to-r from-white via-[#fff3f6] to-[#ffdce5] px-10 py-14 max-[640px]:px-6">
          <div className="relative z-10 max-w-[420px]">
            <h1 className="text-[38px] font-extrabold uppercase text-brand-red max-[640px]:text-[30px]">Shop mỹ phẩm</h1>
            <p className="mt-4 text-[16px] leading-8 text-brand-ink">Khám phá các sản phẩm làm đẹp chính hãng chất lượng cao từ các thương hiệu uy tín.</p>
          </div>
          <div className="absolute right-10 top-1/2 h-[170px] w-[420px] -translate-y-1/2 rounded-full bg-white/60 blur-2xl" />
          <img src="/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/09_shop_phan_phu_infallible.png" alt="" className="absolute right-[17%] top-7 h-[210px] w-[210px] rotate-[-10deg] object-contain opacity-95 max-[820px]:hidden" />
          <img src="/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/08_shop_son_mac_matte_lipstick.png" alt="" className="absolute right-[5%] top-4 h-[230px] w-[230px] rotate-12 object-contain opacity-95 max-[820px]:hidden" />
        </section>

        <div className="grid grid-cols-[270px_1fr] gap-8 max-[980px]:grid-cols-1">
          <aside className="rounded-[10px] border border-[#f5cfd8] bg-white p-5">
            <h2 className="text-[15px] font-extrabold uppercase text-brand-red">Danh mục sản phẩm</h2>
            <div className="mt-4 -mx-5">
              {categories.map(([label, icon], index) => (
                <div key={label} className={`flex items-center gap-3 px-5 py-3 text-[14px] ${index === 0 ? "bg-brand-pale text-brand-red" : "text-brand-ink"}`}>
                  <img src={`${iconBase}/03_icon_thao_tac_san_pham/${icon}`} alt="" className="h-5 w-5 object-contain" />
                  {label}
                </div>
              ))}
            </div>

            <div className="mt-7 border-t border-brand-line pt-5">
              <h3 className="text-[15px] font-extrabold uppercase text-brand-red">Thương hiệu</h3>
              <div className="mt-4 grid gap-3">
                {brands.map((brand, index) => (
                  <label key={brand} className="flex items-center gap-3 text-[14px]">
                    <span className={`grid h-4 w-4 place-items-center rounded border ${index === 0 ? "border-brand-red bg-brand-red" : "border-brand-line"}`}>
                      {index === 0 ? <span className="h-1.5 w-1.5 rounded-full bg-white" /> : null}
                    </span>
                    {brand}
                  </label>
                ))}
              </div>
              <p className="mt-4 text-[14px] font-semibold text-brand-red">Xem thêm⌄</p>
            </div>

            <div className="mt-7 border-t border-brand-line pt-5">
              <h3 className="text-[15px] font-extrabold uppercase text-brand-red">Khoảng giá</h3>
              <div className="mt-6 h-1 rounded-full bg-brand-red" />
              <div className="mt-3 flex justify-between text-[13px] text-brand-muted"><span>0đ</span><span>2.000.000đ</span></div>
              <div className="mt-4 grid grid-cols-2 gap-2 text-center text-[13px]">
                {["0đ - 500k", "500k - 1tr", "1tr - 2tr", "Trên 2tr"].map((price) => <span key={price} className="rounded-full bg-brand-pale px-3 py-2">{price}</span>)}
              </div>
            </div>

            <div className="mt-7 border-t border-brand-line pt-5">
              <h3 className="text-[15px] font-extrabold uppercase text-brand-red">Đánh giá</h3>
              {[116, 78, 35, 12, 5].map((count, index) => (
                <p key={count} className="mt-3 text-[14px]"><span className="mr-2 inline-block h-4 w-4 rounded border border-brand-line align-middle" /> <span className="text-[#ff9f19]">{"★★★★★".slice(0, 5 - index)}</span><span className="text-[#d7c7c7]">{"★★★★★".slice(0, index)}</span> <span className="text-brand-muted">({count})</span></p>
              ))}
            </div>
          </aside>

          <section>
            <div className="mb-5 flex items-center justify-between gap-4 max-[640px]:block">
              <p className="text-[14px] text-brand-ink">Hiển thị 1-12 trong 120 sản phẩm</p>
              <button className="min-h-10 rounded-md border border-brand-line bg-white px-5 text-[14px] text-brand-muted max-[640px]:mt-3">Sắp xếp: Mới nhất ˅</button>
            </div>
            <ProductGrid columns="shop" />
            <div className="mt-8 flex justify-center gap-1 text-[14px]">
              {["‹", "1", "2", "3", "4", "5", "...", "10", "›"].map((item) => <span key={item} className={`grid h-9 min-w-9 place-items-center rounded border border-brand-line px-3 ${item === "1" ? "bg-brand-red text-white" : "bg-white"}`}>{item}</span>)}
            </div>
          </section>
        </div>
      </main>
      <Footer />
    </>
  );
}
