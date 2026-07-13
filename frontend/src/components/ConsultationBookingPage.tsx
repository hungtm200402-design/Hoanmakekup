"use client";

import { FormEvent, useRef, useState } from "react";
import { Header } from "@/components/Header";
import { createBookingSubmissionGate, submitAppointment } from "@/lib/bookingSubmission";

const iconRoot = "/images/products/icon_trang_chu/cut";

export function ConsultationBookingPage() {
  const [message, setMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const submissionGateRef = useRef(createBookingSubmissionGate());

  async function submitForm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const formData = new FormData(form);
    const date = String(formData.get("date"));
    const timeRange = String(formData.get("time"));
    const time = timeRange.slice(0, 5);

    await submissionGateRef.current.run(async () => {
      setSubmitting(true);
      setMessage("Đang gửi đăng ký tư vấn...");

      try {
        const result = await submitAppointment({
          customerName: formData.get("customerName"),
          phone: formData.get("phone"),
          email: "",
          address: String(formData.get("method") ?? ""),
          service: formData.get("service"),
          tone: "Tư vấn riêng",
          note: formData.get("note"),
          startAt: `${date}T${time}:00+07:00`
        });
        if (!result.ok) {
          setMessage(result.message);
          return;
        }

        setMessage("Đăng ký thành công! Hoàn Doãn sẽ liên hệ xác nhận với bạn trong thời gian sớm nhất.");
        form.reset();
      } finally {
        setSubmitting(false);
      }
    });
  }

  return (
    <div className="consult-booking-page">
      <main className="consult-booking-stage">
        <Header embedded bare />

        <section className="consult-booking-card">
          <div className="consult-booking-heading">
            <span>Tư vấn riêng cùng chuyên gia</span>
            <h1>Đăng ký lịch tư vấn</h1>
            <p>Chia sẻ nhu cầu của bạn để Hoàn Doãn chuẩn bị liệu trình phù hợp trước buổi tư vấn.</p>
          </div>

          <div className="consult-booking-progress">
            {["Thông tin", "Thời gian", "Xác nhận"].map((item, index) => (
              <div className={index === 0 ? "active" : ""} key={item}>
                <b>{index + 1}</b><span>{item}</span>
              </div>
            ))}
          </div>

          <form onSubmit={submitForm}>
            <div className="consult-booking-grid">
              <label>
                <b>Họ và tên <i>*</i></b>
                <span><img src={`${iconRoot}/menu_account.png`} alt="" /><input name="customerName" required placeholder="Nhập họ và tên của bạn" /></span>
              </label>
              <label>
                <b>Số điện thoại <i>*</i></b>
                <span><img src={`${iconRoot}/phone.png`} alt="" /><input name="phone" required type="tel" placeholder="Nhập số điện thoại" /></span>
              </label>
              <label>
                <b>Dịch vụ quan tâm <i>*</i></b>
                <span><img src={`${iconRoot}/menu_service.png`} alt="" /><select name="service" required defaultValue=""><option value="" disabled>Chọn dịch vụ cần tư vấn</option><option>Chăm sóc da chuyên sâu</option><option>Phun xăm thẩm mỹ</option><option>Makeup cao cấp</option><option>Body &amp; Relax</option></select></span>
              </label>
              <label>
                <b>Hình thức tư vấn <i>*</i></b>
                <span><img src={`${iconRoot}/phone.png`} alt="" /><select name="method" required defaultValue=""><option value="" disabled>Chọn hình thức tư vấn</option><option>Tư vấn tại salon</option><option>Gọi điện thoại</option><option>Video call</option></select></span>
              </label>
              <label>
                <b>Ngày mong muốn <i>*</i></b>
                <span><img src={`${iconRoot}/menu_booking.png`} alt="" /><input name="date" required type="date" /></span>
              </label>
              <label>
                <b>Khung giờ <i>*</i></b>
                <span><img src={`${iconRoot}/clock.png`} alt="" /><select name="time" required defaultValue=""><option value="" disabled>Chọn khung giờ</option><option>08:00 - 10:00</option><option>10:00 - 12:00</option><option>14:00 - 16:00</option><option>16:00 - 18:00</option><option>18:00 - 20:00</option></select></span>
              </label>
              <label className="wide">
                <b>Nhu cầu của bạn</b>
                <span className="message"><img src={`${iconRoot}/heart.png`} alt="" /><textarea name="note" placeholder="Mô tả tình trạng, mong muốn hoặc điều bạn cần chuyên gia giải đáp..." /></span>
              </label>
            </div>

            <div className="consult-booking-note">
              {[
                ["vip_user_round.png", "Chuyên gia phù hợp", "Sắp xếp đúng chuyên môn"],
                ["clock.png", "Phản hồi nhanh", "Xác nhận trong 30 phút"],
                ["shield.png", "Bảo mật thông tin", "Riêng tư và an toàn"]
              ].map(([icon, title, text]) => (
                <article key={title}><img src={`${iconRoot}/${icon}`} alt="" /><span><b>{title}</b><small>{text}</small></span></article>
              ))}
            </div>

            <label className="consult-booking-consent">
              <input required type="checkbox" /> Tôi đồng ý để Hoàn Doãn liên hệ xác nhận lịch tư vấn.
            </label>

            <button type="submit" disabled={submitting}>{submitting ? "Đang gửi..." : "Xác nhận đăng ký"} <span>→</span></button>
            {message ? <p className="consult-booking-success">{message}</p> : null}
          </form>
        </section>

        <aside className="consult-booking-copy">
          <span>Hoàn Doãn Beauty &amp; Academy</span>
          <h2>Lắng nghe làn da<br />Hiểu điều bạn cần</h2>
          <p>Mỗi buổi tư vấn là một cuộc trò chuyện riêng tư để tìm ra giải pháp làm đẹp phù hợp nhất.</p>
        </aside>
      </main>
    </div>
  );
}
