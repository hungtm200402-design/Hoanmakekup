import { useEffect, useRef, useState } from "react";
import { AdminAppointment, AdminCustomer, AdminOrder, AdminProduct, DashboardData, formatVnd, type UpsertProductPayload } from "@/lib/api";
import { appointmentStatusText, availableAppointmentStatusActions, type AppointmentFilters, type AppointmentStatus } from "@/lib/adminAppointments";
import { availableOrderStatusActions, orderStatusText, type OrderStatus } from "@/lib/adminOrders";

type Props = {
  activeMenu: string;
  dashboard: DashboardData | null;
  appointments: AdminAppointment[];
  orders: AdminOrder[];
  products: AdminProduct[];
  customers: AdminCustomer[];
  appointmentActionMessage?: string;
  updatingAppointmentId?: string;
  appointmentsLoading?: boolean;
  onAppointmentStatusChange?: (id: string, status: AppointmentStatus) => void;
  onAppointmentFiltersChange?: (filters: AppointmentFilters) => void;
  orderActionMessage?: string;
  updatingOrderId?: string;
  onOrderStatusChange?: (id: string, status: OrderStatus) => void;
  productActionMessage?: string;
  updatingProductId?: string;
  onProductSave?: (product: AdminProduct | null, payload: UpsertProductPayload) => Promise<boolean>;
  onProductDelete?: (product: AdminProduct) => Promise<void>;
};

const card = "rounded-2xl border border-[#ffc6d4] bg-white/72 p-5 shadow-[0_14px_32px_rgba(232,50,108,0.12)]";
const outlineButton = "rounded-lg border border-[#ffc6d4] bg-white px-4 py-2 font-bold text-[#e8326c]";

export function AdminView({ activeMenu, dashboard, appointments, orders, products, customers, appointmentActionMessage = "", updatingAppointmentId = "", appointmentsLoading = false, onAppointmentStatusChange, onAppointmentFiltersChange, orderActionMessage = "", updatingOrderId = "", onOrderStatusChange, productActionMessage = "", updatingProductId = "", onProductSave, onProductDelete }: Props) {
  if (activeMenu === "TỔNG QUAN") return <DashboardView dashboard={dashboard} appointments={appointments} orders={orders} products={products} customers={customers} />;
  if (activeMenu === "QUẢN LÝ") return <CustomersView customers={customers} />;
  if (activeMenu === "LỊCH HẸN") return (
    <AppointmentsView
      appointments={appointments}
      actionMessage={appointmentActionMessage}
      updatingAppointmentId={updatingAppointmentId}
      loading={appointmentsLoading}
      onStatusChange={onAppointmentStatusChange}
      onFiltersChange={onAppointmentFiltersChange}
    />
  );
  if (activeMenu === "SẢN PHẨM") return <ProductsView products={products} actionMessage={productActionMessage} updatingProductId={updatingProductId} onSave={onProductSave} onDelete={onProductDelete} />;
  if (activeMenu === "ĐƠN HÀNG") return <OrdersView orders={orders} actionMessage={orderActionMessage} updatingOrderId={updatingOrderId} onStatusChange={onOrderStatusChange} />;
  if (activeMenu === "THỐNG KÊ") return <StatsView dashboard={dashboard} appointments={appointments} orders={orders} products={products} customers={customers} />;
  if (activeMenu === "MARKETING") return <EmptyModule title="Marketing" description="Module marketing chưa có bảng dữ liệu/backend thật. Khi bạn yêu cầu, mình sẽ thêm campaign/coupon/banner API rồi nối vào đây." />;
  if (activeMenu === "CÀI ĐẶT") return <EmptyModule title="Cài đặt" description="Module cài đặt chưa lưu database thật. Hiện chưa hiển thị dữ liệu giả để tránh sai lệch." />;
  return null;
}

function SearchBar({ placeholder }: { placeholder: string }) {
  return (
    <div className="mb-5 flex h-14 w-full max-w-[720px] items-center rounded-xl border border-[#ffc6d4] bg-white/75 px-5 shadow-[0_10px_24px_rgba(232,50,108,0.08)]">
      <input className="w-full bg-transparent outline-none placeholder:text-[#a87980]" placeholder={placeholder} />
      <span className="text-[24px] text-[#e8326c]">⌕</span>
    </div>
  );
}

function StatCard({ icon, label, value, sub }: { icon: string; label: string; value: string; sub?: string }) {
  return (
    <article className={card}>
      <div className="flex items-center gap-5">
        <span className="grid h-16 w-16 place-items-center rounded-xl bg-[#fff0f4] text-[34px] text-[#f33f79]">{icon}</span>
        <span>
          <b className="block text-[13px] uppercase text-[#5f3339]">{label}</b>
          <strong className="mt-2 block text-[27px] text-[#211214]">{value}</strong>
          {sub ? <small className="text-green-600">{sub}</small> : null}
        </span>
      </div>
    </article>
  );
}

function DashboardView({ dashboard, appointments, orders, products, customers }: Omit<Props, "activeMenu">) {
  return (
    <>
      <SearchBar placeholder="Tìm kiếm dữ liệu thật trong hệ thống..." />
      <section className="grid grid-cols-4 gap-4 max-[1300px]:grid-cols-2">
        <StatCard icon="🛍" label="Đơn hàng" value={String(orders.length)} />
        <StatCard icon="🗓" label="Lịch hẹn" value={String(appointments.length)} />
        <StatCard icon="👥" label="Khách hàng" value={String(customers.length)} />
        <StatCard icon="▤" label="Sản phẩm" value={String(products.length)} />
      </section>
      <section className="mt-4 grid grid-cols-[1.4fr_0.9fr_0.9fr] gap-4 max-[1200px]:grid-cols-1">
        <TableCard title="Lịch hẹn mới nhất" headers={["Khách hàng", "SĐT", "Dịch vụ", "Thời gian", "Trạng thái"]} rows={appointments.slice(0, 6).map((item) => [item.customerName, item.phone, item.service, formatDateTime(item.startAt), appointmentStatusText(item.status)])} empty="Chưa có lịch hẹn thật. Hãy đặt lịch từ trang /dat-lich." />
        <ListCard title="Đơn hàng gần đây" items={orders.slice(0, 6).map((order) => `${order.customerName} — ${formatVnd(order.total)} — ${order.status}`)} empty="Chưa có đơn hàng thật." />
        <ListCard title="Khách hàng gần đây" items={customers.slice(0, 6).map((customer) => `${customer.name} — ${customer.phone}`)} empty="Chưa có khách hàng thật." />
      </section>
    </>
  );
}

function CustomersView({ customers }: { customers: AdminCustomer[] }) {
  return (
    <>
      <SearchBar placeholder="Tìm kiếm khách hàng theo tên, SĐT..." />
      <section className="grid grid-cols-4 gap-4 max-[1200px]:grid-cols-2">
        <StatCard icon="👥" label="Tổng khách hàng" value={String(customers.length)} />
        <StatCard icon="↻" label="Tổng lượt tương tác" value={String(customers.reduce((sum, customer) => sum + customer.visits, 0))} />
        <StatCard icon="♕" label="Khách có hoạt động" value={String(customers.filter((customer) => customer.visits > 0).length)} />
        <StatCard icon="◴" label="Mới nhất" value={customers[0] ? formatDate(customers[0].lastActivityAt) : "..."} />
      </section>
      <section className="mt-4">
        <TableCard title="Danh sách khách hàng" headers={["Khách hàng", "Số điện thoại", "Số lần phát sinh", "Hoạt động gần nhất"]} rows={customers.map((customer) => [customer.name, customer.phone, String(customer.visits), formatDateTime(customer.lastActivityAt)])} empty="Chưa có khách hàng thật. Khách sẽ xuất hiện sau khi đặt lịch hoặc đặt hàng." />
      </section>
    </>
  );
}

function AppointmentsView({
  appointments,
  actionMessage,
  updatingAppointmentId,
  loading,
  onStatusChange,
  onFiltersChange
}: {
  appointments: AdminAppointment[];
  actionMessage: string;
  updatingAppointmentId: string;
  loading: boolean;
  onStatusChange?: (id: string, status: AppointmentStatus) => void;
  onFiltersChange?: (filters: AppointmentFilters) => void;
}) {
  const [filters, setFilters] = useState<AppointmentFilters>({});
  const onFiltersChangeRef = useRef(onFiltersChange);

  useEffect(() => {
    onFiltersChangeRef.current = onFiltersChange;
  }, [onFiltersChange]);

  useEffect(() => {
    const timeout = window.setTimeout(() => onFiltersChangeRef.current?.(filters), 300);
    return () => window.clearTimeout(timeout);
  }, [filters]);

  function updateFilter(key: keyof AppointmentFilters, value: string) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  function resetFilters() {
    setFilters({});
  }

  const pending = appointments.filter((item) => item.status === "Pending").length;
  const confirmed = appointments.filter((item) => item.status === "Confirmed").length;
  const completed = appointments.filter((item) => item.status === "Completed").length;
  const cancelled = appointments.filter((item) => item.status === "Cancelled" || item.status === "Rejected").length;
  return (
    <>
      <section className={`${card} mb-5`}>
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 className="font-bold uppercase text-[#e8326c]">Tìm và lọc lịch hẹn</h2>
          <button className={outlineButton} onClick={resetFilters} type="button">Xóa bộ lọc</button>
        </div>
        <div className="grid grid-cols-3 gap-3 max-[900px]:grid-cols-2 max-[580px]:grid-cols-1">
          <input value={filters.customerName ?? ""} onChange={(event) => updateFilter("customerName", event.target.value)} className="rounded-lg border border-[#ffc6d4] bg-white px-3 py-2" placeholder="Tên khách hàng" />
          <input value={filters.phone ?? ""} onChange={(event) => updateFilter("phone", event.target.value)} className="rounded-lg border border-[#ffc6d4] bg-white px-3 py-2" placeholder="Số điện thoại" inputMode="tel" />
          <select value={filters.status ?? ""} onChange={(event) => updateFilter("status", event.target.value)} className="rounded-lg border border-[#ffc6d4] bg-white px-3 py-2">
            <option value="">Tất cả trạng thái</option><option value="Pending">Chờ xác nhận</option><option value="Confirmed">Đã xác nhận</option><option value="Completed">Đã hoàn thành</option><option value="Rejected">Đã từ chối</option><option value="Cancelled">Đã hủy</option>
          </select>
          <input value={filters.service ?? ""} onChange={(event) => updateFilter("service", event.target.value)} className="rounded-lg border border-[#ffc6d4] bg-white px-3 py-2" placeholder="Dịch vụ" />
          <label className="text-[13px] text-[#7b4b50]">Từ ngày<input value={filters.fromDate ?? ""} onChange={(event) => updateFilter("fromDate", event.target.value)} className="mt-1 block w-full rounded-lg border border-[#ffc6d4] bg-white px-3 py-2" type="date" /></label>
          <label className="text-[13px] text-[#7b4b50]">Đến ngày<input value={filters.toDate ?? ""} onChange={(event) => updateFilter("toDate", event.target.value)} className="mt-1 block w-full rounded-lg border border-[#ffc6d4] bg-white px-3 py-2" type="date" /></label>
        </div>
      </section>
      <section className="grid grid-cols-4 gap-4 max-[1000px]:grid-cols-2">
        <StatCard icon="🗓" label="Tổng lịch" value={String(appointments.length)} />
        <StatCard icon="🕘" label="Chờ xác nhận" value={String(pending)} />
        <StatCard icon="✅" label="Đã xác nhận/hoàn thành" value={String(confirmed + completed)} />
        <StatCard icon="❌" label="Đã hủy/từ chối" value={String(cancelled)} />
      </section>
      <section className="mt-4">
        <section className={`${card} overflow-x-auto`}>
          <div className="mb-4 flex items-center justify-between gap-4">
            <h2 className="font-bold uppercase text-[#e8326c]">Danh sách lịch hẹn</h2>
            <button className={outlineButton} onClick={() => onFiltersChange?.(filters)} type="button">Làm mới</button>
          </div>
          {actionMessage ? <p className="mb-4 rounded-xl border border-[#ffc6d4] bg-[#fff5f8] p-3 text-[13px] font-bold text-[#e8326c]">{actionMessage}</p> : null}
          {loading && appointments.length === 0 ? <EmptyState text="Đang tải danh sách lịch hẹn thật..." /> : appointments.length === 0 ? <EmptyState text="Không tìm thấy lịch hẹn phù hợp với bộ lọc." /> : (
            <table className="w-full min-w-[1040px] text-left text-[13px]">
              <thead className="bg-[#fff0f4] text-[#9b4052]">
                <tr>
                  {["Khách hàng", "SĐT", "Dịch vụ", "Tone/Chuyên viên", "Thời gian", "Ghi chú", "Trạng thái", "Thao tác"].map((header) => <th key={header} className="px-4 py-3">{header}</th>)}
                </tr>
              </thead>
              <tbody>
                {appointments.map((item) => {
                  const actions = availableAppointmentStatusActions(item.status);
                  const updating = updatingAppointmentId === item.id;
                  return (
                    <tr key={item.id} className="border-b border-[#f7d5dc] align-top">
                      <td className="px-4 py-3 font-bold text-[#4b2025]">{item.customerName}</td>
                      <td className="px-4 py-3">{item.phone}</td>
                      <td className="px-4 py-3">{item.service}</td>
                      <td className="px-4 py-3">{item.tone || "-"}</td>
                      <td className="px-4 py-3">{formatDateTime(item.startAt)}</td>
                      <td className="max-w-[220px] px-4 py-3">{item.note || "-"}</td>
                      <td className="px-4 py-3"><span className="rounded bg-[#fff2f5] px-2 py-1 text-[#e8326c]">{appointmentStatusText(item.status)}</span></td>
                      <td className="px-4 py-3">
                        {actions.length === 0 ? (
                          <span className="text-[#7b4b50]">Không còn thao tác</span>
                        ) : (
                          <div className="flex flex-wrap gap-2">
                            {actions.map((action) => (
                              <button
                                key={`${item.id}-${action.status}`}
                                onClick={() => onStatusChange?.(item.id, action.status)}
                                disabled={!onStatusChange || updating || Boolean(updatingAppointmentId && !updating)}
                                className={`rounded-lg px-3 py-2 text-[12px] font-bold text-white disabled:cursor-not-allowed disabled:opacity-55 ${action.tone === "danger" ? "bg-[#b04432]" : action.tone === "neutral" ? "bg-[#7b4b50]" : "bg-[#ef3670]"}`}
                                type="button"
                              >
                                {updating ? "Đang lưu..." : action.label}
                              </button>
                            ))}
                          </div>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </section>
      </section>
    </>
  );
}

function ProductsView({
  products,
  actionMessage,
  updatingProductId,
  onSave,
  onDelete
}: {
  products: AdminProduct[];
  actionMessage: string;
  updatingProductId: string;
  onSave?: (product: AdminProduct | null, payload: UpsertProductPayload) => void;
  onDelete?: (product: AdminProduct) => void;
}) {
  const [editing, setEditing] = useState<AdminProduct | null>(null);
  const [creating, setCreating] = useState(false);
  const activeProduct = editing;

  function closeEditor() {
    setEditing(null);
    setCreating(false);
  }

  return (
    <>
      <div className="mb-5 flex items-center justify-between gap-3">
        <h2 className="font-bold uppercase text-[#e8326c]">Sản phẩm từ backend</h2>
        <button className={outlineButton} onClick={() => { setEditing(null); setCreating(true); }} type="button">Thêm sản phẩm</button>
      </div>
      {creating || activeProduct ? <ProductEditor product={activeProduct} saving={Boolean(updatingProductId)} onCancel={closeEditor} onSave={async (payload) => { const saved = await onSave?.(activeProduct, payload); if (saved) closeEditor(); }} /> : null}
      {actionMessage ? <p className="mb-4 rounded-xl border border-[#ffc6d4] bg-[#fff5f8] p-3 text-[13px] font-bold text-[#e8326c]">{actionMessage}</p> : null}
      <section className="grid grid-cols-4 gap-4 max-[1000px]:grid-cols-2">
        <StatCard icon="🛍" label="Tổng sản phẩm" value={String(products.length)} />
        <StatCard icon="⚠" label="Sắp hết hàng" value={String(products.filter((product) => product.stock <= 10).length)} />
        <StatCard icon="%" label="Đang sale" value={String(products.filter((product) => product.salePrice !== null).length)} />
        <StatCard icon="▤" label="Tổng tồn kho" value={String(products.reduce((sum, product) => sum + product.stock, 0))} />
      </section>
      <section className="mt-4">
        <section className={`${card} overflow-x-auto`}>
          {products.length === 0 ? <EmptyState text="Chưa có sản phẩm trong backend." /> : <table className="w-full min-w-[960px] text-left text-[13px]"><thead className="bg-[#fff0f4] text-[#9b4052]"><tr>{["Tên sản phẩm", "Slug", "Giá", "Giá sale", "Tồn kho", "Ảnh", "Thao tác"].map((header) => <th key={header} className="px-4 py-3">{header}</th>)}</tr></thead><tbody>{products.map((product) => <tr key={product.id} className="border-b border-[#f7d5dc]"><td className="px-4 py-3 font-bold text-[#4b2025]">{product.name}</td><td className="px-4 py-3">{product.slug}</td><td className="px-4 py-3">{formatVnd(product.price)}</td><td className="px-4 py-3">{product.salePrice ? formatVnd(product.salePrice) : "-"}</td><td className="px-4 py-3">{product.stock}</td><td className="max-w-[180px] truncate px-4 py-3">{product.imagePath}</td><td className="px-4 py-3"><div className="flex gap-2"><button className={outlineButton} onClick={() => { setEditing(product); setCreating(false); }} type="button">Sửa</button><button className="rounded-lg bg-[#b04432] px-3 py-2 font-bold text-white disabled:opacity-55" disabled={!onDelete || Boolean(updatingProductId)} onClick={() => { if (window.confirm(`Xóa sản phẩm ${product.name}?`)) onDelete?.(product); }} type="button">{updatingProductId === product.id ? "Đang xóa..." : "Xóa"}</button></div></td></tr>)}</tbody></table>}
        </section>
      </section>
    </>
  );
}

function ProductEditor({ product, saving, onCancel, onSave }: { product: AdminProduct | null; saving: boolean; onCancel: () => void; onSave: (payload: UpsertProductPayload) => Promise<void> }) {
  const [form, setForm] = useState({ slug: product?.slug ?? "", name: product?.name ?? "", price: String(product?.price ?? ""), salePrice: product?.salePrice ? String(product.salePrice) : "", saleApproved: product?.saleApproved ?? false, stock: String(product?.stock ?? "0"), imagePath: product?.imagePath ?? "" });
  const [message, setMessage] = useState("");
  function field(key: keyof typeof form, value: string | boolean) { setForm((current) => ({ ...current, [key]: value })); }
  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const price = Number(form.price);
    const stock = Number(form.stock);
    const salePrice = form.salePrice.trim() ? Number(form.salePrice) : null;
    if (!form.slug.trim() || !form.name.trim() || !Number.isFinite(price) || price <= 0 || !Number.isInteger(stock) || stock < 0 || !form.imagePath.trim()) { setMessage("Vui lòng nhập slug, tên, giá hợp lệ, tồn kho không âm và đường dẫn ảnh."); return; }
    if (salePrice !== null && (!Number.isFinite(salePrice) || salePrice <= 0 || salePrice >= price)) { setMessage("Giá sale phải lớn hơn 0 và nhỏ hơn giá gốc."); return; }
    await onSave({ slug: form.slug.trim(), name: form.name.trim(), price, salePrice, saleApproved: form.saleApproved && salePrice !== null, stock, imagePath: form.imagePath.trim() });
  }
  return <form onSubmit={submit} className={`${card} mb-5`}><div className="mb-4 flex items-center justify-between"><h2 className="font-bold uppercase text-[#e8326c]">{product ? `Sửa ${product.name}` : "Thêm sản phẩm"}</h2><button className={outlineButton} onClick={onCancel} type="button">Đóng</button></div><div className="grid grid-cols-3 gap-3 max-[900px]:grid-cols-2 max-[580px]:grid-cols-1"><input value={form.name} onChange={(event) => field("name", event.target.value)} className="rounded-lg border border-[#ffc6d4] px-3 py-2" placeholder="Tên sản phẩm" required /><input value={form.slug} onChange={(event) => field("slug", event.target.value)} className="rounded-lg border border-[#ffc6d4] px-3 py-2" placeholder="slug-san-pham" required /><input value={form.imagePath} onChange={(event) => field("imagePath", event.target.value)} className="rounded-lg border border-[#ffc6d4] px-3 py-2" placeholder="/images/products/ten-anh.png" required /><input value={form.price} onChange={(event) => field("price", event.target.value)} className="rounded-lg border border-[#ffc6d4] px-3 py-2" min="1" type="number" placeholder="Giá gốc" required /><input value={form.salePrice} onChange={(event) => field("salePrice", event.target.value)} className="rounded-lg border border-[#ffc6d4] px-3 py-2" min="1" type="number" placeholder="Giá sale (không bắt buộc)" /><input value={form.stock} onChange={(event) => field("stock", event.target.value)} className="rounded-lg border border-[#ffc6d4] px-3 py-2" min="0" step="1" type="number" placeholder="Tồn kho" required /><label className="flex items-center gap-2 text-[13px] font-bold text-[#7b4b50]"><input checked={form.saleApproved} disabled={!form.salePrice.trim()} onChange={(event) => field("saleApproved", event.target.checked)} type="checkbox" />Áp dụng giá sale</label></div>{message ? <p className="mt-3 text-[13px] font-bold text-[#b04432]">{message}</p> : null}<div className="mt-4"><button className="rounded-lg bg-[#ef3670] px-4 py-2 font-bold text-white disabled:opacity-55" disabled={saving} type="submit">{saving ? "Đang lưu..." : "Lưu sản phẩm"}</button></div></form>;
}

function OrdersView({ orders, actionMessage, updatingOrderId, onStatusChange }: { orders: AdminOrder[]; actionMessage: string; updatingOrderId: string; onStatusChange?: (id: string, status: OrderStatus) => void; }) {
  return (
    <>
      {actionMessage ? <p className="mb-4 rounded-xl border border-[#ffc6d4] bg-[#fff5f8] p-3 text-[13px] font-bold text-[#e8326c]">{actionMessage}</p> : null}
      <section className="grid grid-cols-5 gap-4 max-[1200px]:grid-cols-2">
        <StatCard icon="🛍" label="Tổng đơn" value={String(orders.length)} />
        <StatCard icon="🕘" label="Pending" value={String(orders.filter((order) => order.status === "Pending").length)} />
        <StatCard icon="🚚" label="Shipping" value={String(orders.filter((order) => order.status === "Shipping").length)} />
        <StatCard icon="✅" label="Completed" value={String(orders.filter((order) => order.status === "Completed").length)} />
        <StatCard icon="💰" label="Tổng doanh thu" value={formatVnd(orders.reduce((sum, order) => sum + order.total, 0))} />
      </section>
      <section className="mt-4">
        <section className={`${card} overflow-x-auto`}>
          {orders.length === 0 ? <EmptyState text="Chưa có đơn hàng thật. Hãy tạo đơn từ trang thanh toán." /> : <table className="w-full min-w-[1080px] text-left text-[13px]"><thead className="bg-[#fff0f4] text-[#9b4052]"><tr>{["Mã đơn", "Khách hàng", "SĐT", "Sản phẩm", "Giá trị", "Thời gian", "Trạng thái", "Thao tác"].map((header) => <th key={header} className="px-4 py-3">{header}</th>)}</tr></thead><tbody>{orders.map((order) => { const actions = availableOrderStatusActions(order.status); const updating = updatingOrderId === order.id; return <tr key={order.id} className="border-b border-[#f7d5dc] align-top"><td className="px-4 py-3 font-bold text-[#4b2025]">{shortId(order.id)}</td><td className="px-4 py-3">{order.customerName}</td><td className="px-4 py-3">{order.phone}</td><td className="max-w-[260px] px-4 py-3">{order.items.map((item) => `${item.productName} x${item.quantity}`).join(", ")}</td><td className="px-4 py-3">{formatVnd(order.total)}</td><td className="px-4 py-3">{formatDateTime(order.createdAt)}</td><td className="px-4 py-3"><span className="rounded bg-[#fff2f5] px-2 py-1 text-[#e8326c]">{orderStatusText(order.status)}</span></td><td className="px-4 py-3">{actions.length === 0 ? <span className="text-[#7b4b50]">Không còn thao tác</span> : <div className="flex flex-wrap gap-2">{actions.map((action) => <button key={action.status} onClick={() => onStatusChange?.(order.id, action.status)} disabled={!onStatusChange || Boolean(updatingOrderId)} className={`rounded-lg px-3 py-2 text-[12px] font-bold text-white disabled:opacity-55 ${action.tone === "danger" ? "bg-[#b04432]" : "bg-[#ef3670]"}`} type="button">{updating ? "Đang lưu..." : action.label}</button>)}</div>}</td></tr>; })}</tbody></table>}
        </section>
      </section>
    </>
  );
}

function StatsView({ dashboard, appointments, orders, products, customers }: Omit<Props, "activeMenu">) {
  return (
    <>
      <section className="grid grid-cols-5 gap-4 max-[1300px]:grid-cols-3">
        <StatCard icon="🛍" label="Đơn hàng" value={String(orders.length)} />
        <StatCard icon="🗓" label="Lịch hẹn" value={String(appointments.length)} />
        <StatCard icon="👥" label="Khách hàng" value={String(customers.length)} />
        <StatCard icon="▤" label="Sản phẩm" value={String(products.length)} />
        <StatCard icon="📦" label="Tồn kho" value={String(products.reduce((sum, product) => sum + product.stock, 0))} />
      </section>
      <section className="mt-4 grid grid-cols-2 gap-4 max-[1000px]:grid-cols-1">
        <ListCard title="Top khách hàng thật" items={(dashboard?.topCustomers ?? []).map((item) => `${item.name} — ${item.count} lần`)} empty="Chưa có top khách hàng." />
        <ListCard title="Sản phẩm tồn kho cao" items={products.slice().sort((a, b) => b.stock - a.stock).slice(0, 8).map((product) => `${product.name} — ${product.stock}`)} empty="Chưa có sản phẩm." />
      </section>
    </>
  );
}

function EmptyModule({ title, description }: { title: string; description: string }) {
  return (
    <section className={`${card} min-h-[360px]`}>
      <h2 className="text-[24px] font-bold uppercase text-[#e8326c]">{title}</h2>
      <p className="mt-4 max-w-[720px] leading-7 text-[#7b4b50]">{description}</p>
      <p className="mt-5 rounded-xl bg-[#fff5f7] p-4 text-[14px] text-[#9b4052]">Không hiển thị dữ liệu ảo. Khi backend có bảng/API thật cho module này, trang admin sẽ nối vào ngay.</p>
    </section>
  );
}

function TableCard({ title, headers, rows, empty }: { title: string; headers: string[]; rows: string[][]; empty: string }) {
  return (
    <section className={`${card} overflow-x-auto`}>
      <div className="mb-4 flex justify-between"><h2 className="font-bold uppercase text-[#e8326c]">{title}</h2><button className={outlineButton}>Làm mới</button></div>
      {rows.length === 0 ? <EmptyState text={empty} /> : (
        <table className="w-full min-w-[860px] text-left text-[13px]">
          <thead className="bg-[#fff0f4] text-[#9b4052]"><tr>{headers.map((header) => <th key={header} className="px-4 py-3">{header}</th>)}</tr></thead>
          <tbody>{rows.map((row) => <tr key={row.join("-")} className="border-b border-[#f7d5dc]">{row.map((cell, index) => <td key={`${cell}-${index}`} className="px-4 py-3">{index === row.length - 1 ? <span className="rounded bg-[#fff2f5] px-2 py-1 text-[#e8326c]">{cell}</span> : cell}</td>)}</tr>)}</tbody>
        </table>
      )}
    </section>
  );
}

function ListCard({ title, items, empty }: { title: string; items: string[]; empty: string }) {
  return (
    <section className={card}>
      <div className="mb-4 flex justify-between"><h2 className="font-bold uppercase text-[#e8326c]">{title}</h2></div>
      {items.length === 0 ? <EmptyState text={empty} /> : <div className="grid gap-3">{items.map((item, index) => <p key={item} className="flex items-center justify-between rounded-lg bg-[#fff7f8] p-3 text-[13px]"><span>{item}</span><b className="text-[#f33f79]">{index + 1}</b></p>)}</div>}
    </section>
  );
}

function EmptyState({ text }: { text: string }) {
  return <div className="rounded-xl border border-dashed border-[#ffc6d4] bg-[#fff8f9] p-6 text-center text-[#9b4052]">{text}</div>;
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" });
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString("vi-VN");
}

function shortId(value: string) {
  return value.slice(0, 8).toUpperCase();
}
