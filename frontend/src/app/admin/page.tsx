"use client";

import { useEffect, useState } from "react";
import { Header } from "@/components/Header";
import { appointmentRows, orderRows, productRows } from "@/lib/data";
import { DashboardData, fetchDashboard, formatVnd } from "@/lib/api";

const tabs = ["Tổng quan", "Lịch hẹn", "Đơn hàng", "Sản phẩm", "Khách hàng", "AI bản nháp"];

function Table({ rows, headers }: { rows: string[][]; headers: string[] }) {
  return (
    <div className="overflow-x-auto rounded border border-brand-line bg-white">
      <table className="w-full min-w-[680px] border-collapse text-left text-[14px]">
        <thead className="bg-brand-pale">
          <tr>{headers.map((header) => <th key={header} className="border-b border-brand-line px-5 py-4">{header}</th>)}</tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.join("-")}>
              {row.map((cell, index) => (
                <td key={`${cell}-${index}`} className="border-b border-brand-line px-5 py-4">
                  {index === row.length - 1 ? <span className="rounded-full bg-brand-soft px-3 py-1 text-[12px] font-bold text-brand-red">{cell}</span> : cell}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default function AdminPage() {
  const [tab, setTab] = useState("Tổng quan");
  const [dashboard, setDashboard] = useState<DashboardData | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    fetchDashboard()
      .then(setDashboard)
      .catch((exception: Error) => setError(exception.message));
  }, []);

  return (
    <>
      <Header />
      <main className="min-h-screen bg-[#f7f8fa]">
        <div className="grid grid-cols-[220px_1fr] max-[900px]:grid-cols-1">
          <aside className="bg-[#121c29] px-5 py-7 text-white max-[900px]:hidden">
            <div className="text-[25px] font-semibold italic text-white">Hoàn Makeup</div>
            <p className="mt-1 text-[11px] tracking-[0.25em] text-white/55">ADMINISTRATOR</p>
            <nav className="mt-9 grid gap-2 text-[14px]">
              {tabs.slice(0, 5).map((item) => <button key={item} onClick={() => setTab(item)} className={`rounded px-4 py-3 text-left ${tab === item ? "bg-white/12" : ""}`} type="button">{item}</button>)}
            </nav>
          </aside>
          <section className="p-8 max-[520px]:p-4">
            <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
              <div>
                <h1 className="text-[28px] font-bold uppercase">Tổng quan</h1>
                <p className="mt-1 text-[13px] text-brand-muted">Quản lý lịch, đơn hàng, sản phẩm, khách hàng và AI bản nháp.</p>
              </div>
              <div className="flex gap-3 text-[13px]">🔔 👤</div>
            </div>
            <div className="mb-7 hidden gap-2 overflow-x-auto max-[900px]:flex">
              {tabs.map((item) => <button key={item} onClick={() => setTab(item)} className={`shrink-0 rounded border px-4 py-2 text-[13px] font-bold ${tab === item ? "border-brand-red bg-brand-red text-white" : "border-brand-line bg-white"}`} type="button">{item}</button>)}
            </div>

            {tab === "Tổng quan" ? (
              <>
                {error ? <p className="mb-5 rounded border border-brand-line bg-brand-pale p-4 text-brand-red">{error}</p> : null}
                <div className="grid grid-cols-4 gap-5 max-[1180px]:grid-cols-2 max-[520px]:grid-cols-1">
                  {[
                    [dashboard ? formatVnd(dashboard.revenueToday) : "...", "Doanh thu hôm nay", "Dữ liệu từ backend"],
                    [dashboard ? String(dashboard.newOrders) : "...", "Đơn hàng mới", "Dữ liệu từ backend"],
                    [dashboard ? String(dashboard.appointmentsToday) : "...", "Lịch hẹn hôm nay", "Dữ liệu từ backend"],
                    [dashboard ? String(dashboard.newCustomers) : "...", "Khách hàng mới", "Dữ liệu từ backend"]
                  ].map(([value, label, sub]) => (
                    <article key={label} className="rounded border border-brand-line bg-white p-6">
                      <strong className="text-[24px]">{value}</strong>
                      <p className="mt-2 font-bold">{label}</p>
                      <p className="mt-2 text-[12px] text-green-600">{sub}</p>
                    </article>
                  ))}
                </div>
                <div className="mt-7 grid grid-cols-[1.2fr_0.8fr] gap-6 max-[1180px]:grid-cols-1">
                  <section className="rounded border border-brand-line bg-white p-6">
                    <h2 className="mb-5 text-[18px] font-bold uppercase">Doanh thu</h2>
                    <div className="flex h-[260px] items-end gap-5 border-b border-l border-brand-line px-4">
                      {[44, 58, 75, 54, 88, 66, 80].map((height, index) => <span key={index} className="w-full rounded-t bg-[#8db4ff]" style={{ height: `${height}%` }} />)}
                    </div>
                  </section>
                  <section className="rounded border border-brand-line bg-white p-6">
                    <h2 className="mb-5 text-[18px] font-bold uppercase">Lịch hẹn hôm nay</h2>
                    <div className="grid gap-4">
                      {(dashboard?.appointments ?? []).map((row) => <p key={`${row.startAt}-${row.customerName}`} className="flex justify-between gap-3 text-[14px]"><span>{new Date(row.startAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })} - {row.customerName}</span><strong>{row.service}</strong></p>)}
                      {dashboard && dashboard.appointments.length === 0 ? <p className="text-[14px] text-brand-muted">Chưa có lịch hẹn.</p> : null}
                    </div>
                  </section>
                </div>
                <div className="mt-7 grid grid-cols-2 gap-6 max-[820px]:grid-cols-1">
                  <section className="rounded border border-brand-line bg-white p-6">
                    <h2 className="mb-5 text-[18px] font-bold uppercase">Sản phẩm bán chạy</h2>
                    <div className="grid gap-3 text-[14px]">
                      {(dashboard?.bestProducts ?? []).map((item) => <p key={item.name} className="flex justify-between"><span>{item.name}</span><strong>{item.stock}</strong></p>)}
                    </div>
                  </section>
                  <section className="rounded border border-brand-line bg-white p-6">
                    <h2 className="mb-5 text-[18px] font-bold uppercase">Top khách hàng</h2>
                    <div className="grid gap-3 text-[14px]">
                      {(dashboard?.topCustomers ?? []).map((item) => <p key={item.name} className="flex justify-between"><span>{item.name}</span><strong>{item.count}</strong></p>)}
                    </div>
                  </section>
                </div>
              </>
            ) : null}

            {tab === "Lịch hẹn" ? <Table headers={["Giờ", "Khách hàng", "Địa điểm", "Trạng thái"]} rows={appointmentRows} /> : null}
            {tab === "Đơn hàng" ? <Table headers={["Mã đơn", "Sản phẩm", "Giá trị", "Trạng thái"]} rows={orderRows} /> : null}
            {tab === "Sản phẩm" ? <Table headers={["Mã SP", "Tên sản phẩm", "Tồn kho", "Trạng thái"]} rows={productRows} /> : null}
            {tab === "Khách hàng" ? <Table headers={["Mã KH", "Tên khách", "Số điện thoại", "Trạng thái"]} rows={[["KH001", "Nguyễn Thị Hòa", "0123 456 789", "Hoạt động"], ["KH002", "Trần Mỹ Mai", "0987 654 321", "Hoạt động"]]} /> : null}
            {tab === "AI bản nháp" ? (
              <section className="grid grid-cols-2 gap-6 max-[820px]:grid-cols-1">
                <article className="rounded border border-brand-line bg-white p-6">
                  <h2 className="text-[18px] font-bold">AI tạo bài sale sản phẩm</h2>
                  <textarea className="mt-5 min-h-[170px] w-full resize-none border border-brand-line p-4" defaultValue="Bản nháp: Son Black Rouge Air Fit với sắc đỏ cam nổi bật, chất son mềm mịn, phù hợp makeup hằng ngày." />
                  <p className="mt-3 text-[12px] text-brand-muted">Chỉ lưu bản nháp. Admin phải duyệt trước khi đăng hoặc kích hoạt sale.</p>
                </article>
                <article className="rounded border border-brand-line bg-white p-6">
                  <h2 className="text-[18px] font-bold">AI tạo bài đăng từ ảnh khách makeup</h2>
                  <textarea className="mt-5 min-h-[170px] w-full resize-none border border-brand-line p-4" defaultValue="Bản nháp: Layout makeup trong trẻo, nhấn vào đôi mắt và lớp nền mịn nhẹ." />
                  <p className="mt-3 text-[12px] text-brand-muted">Ảnh khách được lưu riêng tư; chỉ staff/admin được xem và duyệt nội dung.</p>
                </article>
              </section>
            ) : null}
          </section>
        </div>
      </main>
    </>
  );
}
