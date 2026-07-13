"use client";

import { useState } from "react";
import { Header } from "@/components/Header";

const assetRoot = "/images/products/HoanDoan_Assets_TheoTungTrang_Gon/HoanDoan_Assets_TheoTungTrang_Gon";
const iconRoot = "/images/products/icon_trang_chu/cut";

const basicFields = [
  ["Họ và tên", "Nhập họ và tên của bạn", "menu_account.png", "text"],
  ["Số điện thoại", "Nhập số điện thoại", "phone.png", "tel"],
  ["Email", "Nhập email của bạn", "menu_cart.png", "email"],
  ["Ngày sinh", "Chọn ngày sinh", "menu_booking.png", "date"],
  ["Khu vực sinh sống", "Chọn khu vực của bạn", "menu_booking.png", "text"],
  ["Nghề nghiệp", "Nhập nghề nghiệp của bạn", "menu_combo.png", "text"]
];

const conditionFields = [
  ["Doanh số tích lũy", "Nhập doanh số tích lũy", "vip_60fps.png", "từ 5.000.000đ"],
  ["Số đơn hàng", "Nhập số đơn hàng", "menu_cart.png", "từ 3 đơn"],
  ["Thời gian đồng hành", "Nhập thời gian đồng hành", "menu_booking.png", "từ 3 tháng"],
  ["Hạng thành viên / Thành viên thân thiết", "Chọn hạng thành viên", "diamond.png", "Thành viên thân thiết Hoàn Doãn"]
];

export function VipSupportPage() {
  const [step, setStep] = useState(1);

  return (
    <div className="vip-support-page">
      <main className="vip-support-stage">
        <Header embedded bare />

        <section className="vip-support-layout">
          <aside className="vip-support-visual">
            <div className="vip-support-title">
              <img src={`${iconRoot}/vip_crown.png`} alt="" />
              <h1>Hỗ trợ<br />khách hàng VIP</h1>
              <em>Ưu tiên phản hồi trong</em>
              <strong>30 PHÚT</strong>
              <span>dành riêng cho bạn</span>
            </div>
            <img className="vip-support-gift" src={`${assetRoot}/04_combo_uu_dai/assets/hop_qua.png`} alt="Hộp quà VIP" />
            <div className="vip-support-benefits">
              {[
                ["phone.png", "Phản hồi nhanh", "Ưu tiên phản hồi trong 30 phút"],
                ["diamond.png", "Ưu đãi đặc quyền", "Nhận ưu đãi và quà tặng dành riêng cho bạn"],
                ["shield.png", "Bảo mật tuyệt đối", "Thông tin của bạn được bảo mật 100%"]
              ].map(([icon, title, text]) => (
                <article key={title}>
                  <img src={`${iconRoot}/${icon}`} alt="" />
                  <h3>{title}</h3>
                  <p>{text}</p>
                </article>
              ))}
            </div>
          </aside>

          <section className="vip-support-form">
            <div className="vip-support-heading">
              <h2>{step === 1 ? "Đăng ký hỗ trợ khách hàng VIP" : step === 2 ? "Kiểm tra điều kiện VIP" : "Xác nhận thông tin VIP"}</h2>
              <p>{step === 1 ? "Điền thông tin để chúng tôi kiểm tra và xác nhận ưu tiên VIP cho bạn" : step === 2 ? "Vui lòng cung cấp thông tin để hệ thống kiểm tra điều kiện VIP của bạn" : "Vui lòng kiểm tra lại toàn bộ thông tin trước khi gửi đăng ký"}</p>
            </div>

            <div className="vip-support-steps">
              {["Thông tin cơ bản", "Kiểm tra điều kiện", "Xác nhận"].map((label, index) => {
                const number = index + 1;
                return (
                  <div key={label} className={step >= number ? "active" : ""}>
                    <span>{step > number ? "✓" : number}</span>
                    <b>{label}</b>
                  </div>
                );
              })}
            </div>

            {step === 1 ? (
              <div className="vip-step-one">
                <h3><img src={`${iconRoot}/menu_account.png`} alt="" />Thông tin cơ bản</h3>
                <div className="vip-basic-grid">
                  {basicFields.map(([label, placeholder, icon, type], index) => (
                    <label key={label} className={index > 3 ? "wide" : ""}>
                      <b>{label}{index !== 5 ? <i>*</i> : null}</b>
                      <span><img src={`${iconRoot}/${icon}`} alt="" /><input type={type} placeholder={placeholder} /></span>
                    </label>
                  ))}
                </div>
                <div className="vip-criteria">
                  <h4>Bạn đã đủ điều kiện trở thành khách hàng VIP?</h4>
                  <p>Vui lòng cung cấp thêm một số thông tin để chúng tôi kiểm tra điều kiện VIP của bạn.</p>
                  <div>
                    {conditionFields.map(([title, , icon, note]) => (
                      <article key={title}><img src={`${iconRoot}/${icon}`} alt="" /><span>{note}</span></article>
                    ))}
                  </div>
                </div>
                <label className="vip-consent"><input type="checkbox" /> Tôi đồng ý với các <a>điều khoản</a> và <a>chính sách bảo mật</a> của Hoàn Doãn Beauty &amp; Academy</label>
                <button className="vip-primary" type="button" onClick={() => setStep(2)}>Tiếp tục kiểm tra điều kiện <span>→</span></button>
                <small><img src={`${iconRoot}/shield.png`} alt="" />Thông tin của bạn được bảo mật tuyệt đối</small>
              </div>
            ) : null}

            {step === 2 ? (
              <div className="vip-step-two">
                <div className="vip-condition-grid">
                  {conditionFields.map(([title, placeholder, icon, note], index) => (
                    <label key={title}>
                      <b><img src={`${iconRoot}/${icon}`} alt="" />{title}<i>*</i></b>
                      {index === 3 ? (
                        <select defaultValue=""><option value="" disabled>{placeholder}</option><option>VIP Bạch Kim</option><option>VIP Vàng</option><option>Thành viên thân thiết</option></select>
                      ) : <input placeholder={placeholder} />}
                      <span>{note}</span>
                    </label>
                  ))}
                </div>
                <label className="vip-consent"><input type="checkbox" /> Tôi xác nhận các thông tin trên là <a>chính xác</a> và <a>trung thực</a>.</label>
                <div className="vip-actions">
                  <button type="button" onClick={() => setStep(1)}>Quay lại</button>
                  <button className="vip-primary" type="button" onClick={() => setStep(3)}>Tiếp tục <span>→</span></button>
                </div>
              </div>
            ) : null}

            {step === 3 ? (
              <div className="vip-step-three">
                <div className="vip-summary">
                  <section>
                    {[
                      ["menu_account.png", "Họ và tên", "Nguyễn Thị Hương"],
                      ["phone.png", "Số điện thoại", "0901 234 567"],
                      ["menu_cart.png", "Email", "huong.nguyen@gmail.com"],
                      ["menu_booking.png", "Ngày sinh", "15/06/1992"],
                      ["menu_booking.png", "Khu vực", "Hà Nội"],
                      ["menu_combo.png", "Nghề nghiệp", "Chủ spa / Thẩm mỹ viện"]
                    ].map(([icon, label, value]) => <p key={label}><img src={`${iconRoot}/${icon}`} alt="" /><span>{label}</span><b>{value}</b></p>)}
                  </section>
                  <section>
                    {[
                      ["vip_60fps.png", "Doanh số tích lũy", "8.750.000đ"],
                      ["menu_cart.png", "Số đơn hàng", "28 đơn"],
                      ["menu_booking.png", "Thời gian đồng hành", "14 tháng"],
                      ["diamond.png", "Hạng thành viên", "VIP Bạch Kim"]
                    ].map(([icon, label, value]) => <p key={label}><img src={`${iconRoot}/${icon}`} alt="" /><span>{label}</span><b>{value}</b></p>)}
                  </section>
                </div>
                <div className="vip-result">
                  <img src={`${iconRoot}/vip_shield_round.png`} alt="" />
                  <section><span>Kết quả kiểm tra:</span><h3>Đủ điều kiện VIP 👑</h3><p>Chúc mừng! Bạn đủ điều kiện trở thành khách hàng VIP của Hoàn Doãn Beauty &amp; Academy.</p></section>
                </div>
                <label className="vip-consent"><input type="checkbox" defaultChecked /> Tôi đồng ý với các <a>điều khoản</a> và <a>chính sách bảo mật</a> của Hoàn Doãn Beauty &amp; Academy</label>
                <div className="vip-actions">
                  <button type="button" onClick={() => setStep(2)}>Quay lại</button>
                  <button className="vip-primary" type="button">Gửi đăng ký <span>→</span></button>
                </div>
              </div>
            ) : null}
          </section>
        </section>
      </main>
    </div>
  );
}
