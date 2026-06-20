"use client";

import { useMemo, useState } from "react";
import { iconBase, services } from "@/lib/data";

const times = ["08:00", "09:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00", "17:00", "18:00", "19:00"];
const dates = [26, 27, 28, 29, 30, 31, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 1, 2, 3, 4, 5, 6];

export function BookingForm() {
  const [serviceSlug, setServiceSlug] = useState(services[0].slug);
  const [time, setTime] = useState("13:00");
  const selected = useMemo(() => services.find((service) => service.slug === serviceSlug) ?? services[0], [serviceSlug]);

  return (
    <div>
      <div className="mx-auto mb-8 grid max-w-[840px] grid-cols-[1fr_1fr_1fr] items-start gap-0 max-[760px]:grid-cols-1 max-[760px]:gap-5">
        {["Chọn dịch vụ", "Chọn ngày & giờ", "Thông tin khách hàng"].map((label, index) => (
          <div key={label} className="relative text-center">
            <div className={`mx-auto grid h-12 w-12 place-items-center rounded-full border-4 text-[22px] font-bold ${index === 0 ? "border-brand-red bg-brand-red text-white" : "border-[#dddddd] bg-white text-brand-muted"}`}>{index + 1}</div>
            <p className={`mt-3 text-[13px] font-extrabold uppercase ${index === 0 ? "text-brand-red" : "text-brand-muted"}`}>{label}</p>
            {index < 2 ? <span className={`absolute left-[58%] top-6 h-[3px] w-[85%] max-[760px]:hidden ${index === 0 ? "bg-brand-red" : "bg-[#dddddd]"}`} /> : null}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-[1fr_1fr_1fr] gap-6 max-[1120px]:grid-cols-1">
        <section className="rounded-[14px] border border-[#f5cfd8] bg-white p-5 shadow-[0_12px_30px_rgba(217,46,85,0.08)]">
          <h2 className="font-serif text-[22px] font-bold uppercase text-brand-red">1. Chọn dịch vụ</h2>
          <div className="mt-5 grid gap-4">
            {services.slice(0, 4).map((service) => {
              const active = service.slug === serviceSlug;
              return (
                <button key={service.slug} type="button" onClick={() => setServiceSlug(service.slug)} className={`grid grid-cols-[26px_118px_1fr] gap-4 rounded-[8px] border p-3 text-left transition ${active ? "border-brand-red bg-brand-pale" : "border-brand-line bg-white"}`}>
                  <span className={`mt-9 grid h-5 w-5 place-items-center rounded-full border ${active ? "border-brand-red bg-brand-red" : "border-brand-line"}`}>{active ? <span className="h-2 w-2 rounded-full bg-white" /> : null}</span>
                  <img src={service.bookingImage ?? service.image} alt={service.title} className="h-[118px] w-[118px] rounded-md object-cover" />
                  <span>
                    <strong className="block text-[16px] uppercase text-brand-red">{service.title}</strong>
                    <strong className="mt-2 block text-[15px]">{service.price}</strong>
                    <span className="mt-2 block text-[13px] leading-5 text-brand-muted">{service.desc}</span>
                  </span>
                </button>
              );
            })}
          </div>
          <button className="mt-5 grid min-h-11 w-full place-items-center rounded-md border border-brand-red text-[14px] font-bold uppercase text-brand-red" type="button">
            <span>▣ Tư vấn dịch vụ</span>
          </button>
        </section>

        <section className="rounded-[14px] border border-[#f5cfd8] bg-white p-5 shadow-[0_12px_30px_rgba(217,46,85,0.08)]">
          <h2 className="font-serif text-[22px] font-bold uppercase text-brand-red">2. Chọn ngày & giờ</h2>
          <h3 className="mt-5 text-[14px] font-extrabold uppercase">Chọn ngày</h3>
          <div className="mt-3 rounded-[8px] border border-brand-line p-4">
            <div className="mb-5 flex items-center justify-between text-[16px] font-bold"><span>‹</span><span>Tháng 6 - 2025</span><span>›</span></div>
            <div className="grid grid-cols-7 gap-2 text-center text-[13px]">
              {["T2", "T3", "T4", "T5", "T6", "T7", "CN"].map((day) => <strong key={day}>{day}</strong>)}
              {dates.map((date, index) => <span key={`${date}-${index}`} className={`grid h-9 place-items-center rounded-full ${date === 18 ? "bg-brand-red font-bold text-white" : index < 6 || index > 34 ? "text-[#bdbdbd]" : "text-brand-ink"}`}>{date}</span>)}
            </div>
          </div>
          <h3 className="mt-5 text-[14px] font-extrabold uppercase">Chọn giờ</h3>
          <div className="mt-3 grid grid-cols-4 gap-3">
            {times.map((item) => <button key={item} onClick={() => setTime(item)} className={`min-h-10 rounded-md border text-[14px] ${item === time ? "border-brand-red bg-brand-red text-white" : "border-brand-line bg-white"}`} type="button">{item}</button>)}
          </div>
          <p className="mt-6 rounded-md bg-brand-pale p-4 text-[13px] leading-6 text-brand-red">ⓘ Vui lòng đến sớm 15 phút để chúng tôi chuẩn bị tốt nhất cho bạn.</p>
        </section>

        <section className="rounded-[14px] border border-[#f5cfd8] bg-white p-5 shadow-[0_12px_30px_rgba(217,46,85,0.08)]">
          <h2 className="font-serif text-[22px] font-bold uppercase text-brand-red">3. Thông tin đặt lịch</h2>
          <h3 className="mt-5 text-[15px] font-extrabold uppercase">Tóm tắt đơn lịch</h3>
          <div className="mt-5 grid grid-cols-[120px_1fr] gap-4">
            <img src={selected.bookingImage ?? selected.image} alt={selected.title} className="h-[120px] w-[120px] rounded-md object-cover" />
            <div>
              <p className="text-[18px] font-extrabold uppercase text-brand-red">{selected.title}</p>
              <p className="mt-2 text-[18px] font-extrabold text-brand-red">{selected.price}</p>
            </div>
          </div>
          <div className="mt-6 grid gap-5 border-y border-brand-line py-6 text-[14px]">
            {[
              ["Ngày", "Thứ Tư, 18/06/2025", `${iconBase}/04_icon_tai_khoan_lien_he/02_lich_hen.png`],
              ["Giờ", time, `${iconBase}/04_icon_tai_khoan_lien_he/01_dong_ho.png`],
              ["Chuyên viên", "Chuyên viên cao cấp", `${iconBase}/04_icon_tai_khoan_lien_he/15_nguoi_dung.png`],
              ["Địa điểm", "Hoàn Doãn Beauty & Academy, 123 Nguyễn Trãi, Q.1, TP. Hồ Chí Minh", `${iconBase}/04_icon_tai_khoan_lien_he/03_vi_tri.png`]
            ].map(([label, value, icon]) => (
              <div key={label} className="grid grid-cols-[32px_80px_1fr] gap-2">
                <img src={icon} alt="" className="h-6 w-6 object-contain" />
                <strong className="text-brand-red">{label}</strong>
                <span className="leading-6">{value}</span>
              </div>
            ))}
          </div>
          <div className="mt-6 flex items-center justify-between">
            <strong className="uppercase">Thành tiền</strong>
            <strong className="text-[24px] text-brand-red">{selected.price}</strong>
          </div>
          <button className="mt-6 min-h-12 w-full rounded-md bg-brand-red text-[16px] font-extrabold uppercase text-white" type="button">Tiếp tục →</button>
          <button className="mt-3 min-h-12 w-full rounded-md border border-brand-red text-[16px] font-extrabold uppercase text-brand-red" type="button">Quay lại</button>
        </section>
      </div>
    </div>
  );
}
