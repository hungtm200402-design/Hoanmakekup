export const iconBase = "/images/products/icon_hoan_doan_lam_lai_chuan";
export const assetBase = "/images/products/hoan_doan_mau_make_san_pham_va_banner";

export const navItems = [
  { href: "/", label: "TRANG CHỦ", icon: `${iconBase}/01_icon_menu/01_trang_chu.png` },
  { href: "/dich-vu-makeup", label: "DỊCH VỤ", icon: `${iconBase}/01_icon_menu/02_dich_vu.png` },
  { href: "/shop-my-pham", label: "SẢN PHẨM", icon: `${iconBase}/01_icon_menu/03_san_pham.png` },
  { href: "/combo-uu-dai", label: "COMBO ƯU ĐÃI", icon: `${iconBase}/01_icon_menu/04_combo_uu_dai.png` },
  { href: "/dat-lich", label: "ĐẶT LỊCH", icon: `${iconBase}/01_icon_menu/05_dat_lich.png` },
  { href: "/tai-khoan", label: "TÀI KHOẢN", icon: `${iconBase}/01_icon_menu/06_tai_khoan.png` },
  { href: "/gio-hang", label: "GIỎ HÀNG", icon: `${iconBase}/01_icon_menu/07_gio_hang.png`, badge: "2" }
];

export const services = [
  { slug: "makeup-co-dau", title: "MAKEUP CÔ DÂU", price: "3.500.000đ", duration: "2.5 - 3 giờ", desc: "Lớp nền trong trẻo, bền đẹp suốt cả ngày trọng đại.", icon: `${iconBase}/02_icon_dich_vu_uy_tin/05_guong_mat_hoa_sen.png`, image: "/images/products/5_anh_makeup/01_makeup_co_dau.png", bookingImage: "/images/products/5_anh_makeup/01_makeup_co_dau.png" },
  { slug: "makeup-du-tiec", title: "MAKEUP DỰ TIỆC", price: "1.800.000đ", duration: "1.5 - 2 giờ", desc: "Makeup sang trọng, cuốn hút cho mọi buổi tiệc.", icon: `${iconBase}/02_icon_dich_vu_uy_tin/11_son_moi.png`, image: "/images/products/5_anh_makeup/02_makeup_du_tiec.png", bookingImage: "/images/products/5_anh_makeup/02_makeup_du_tiec.png" },
  { slug: "makeup-tai-nha", title: "MAKEUP TẠI NHÀ", price: "2.000.000đ", duration: "1.5 - 2 giờ", desc: "Chuyên viên đến tận nơi, tiết kiệm thời gian di chuyển.", icon: `${iconBase}/02_icon_dich_vu_uy_tin/03_chuyen_vien.png`, image: "/images/products/5_anh_makeup/03_makeup_tai_nha.png", bookingImage: "/images/products/5_anh_makeup/03_makeup_tai_nha.png" },
  { slug: "makeup-chup-anh", title: "MAKEUP CHỤP ẢNH", price: "1.500.000đ", duration: "1 - 1.5 giờ", desc: "Tôn lên đường nét, phù hợp với ánh sáng studio và concept.", icon: `${iconBase}/02_icon_dich_vu_uy_tin/07_lap_lanh.png`, image: "/images/products/5_anh_makeup/04_makeup_chup_anh.png", bookingImage: "/images/products/5_anh_makeup/04_makeup_chup_anh.png" },
  { slug: "makeup-su-kien", title: "MAKEUP SỰ KIỆN", price: "2.500.000đ", duration: "2 - 2.5 giờ", desc: "Phong cách chuyên nghiệp, tự tin trong mọi sự kiện.", icon: `${iconBase}/02_icon_dich_vu_uy_tin/08_trai_tim.png`, image: "/images/products/5_anh_makeup/05_makeup_su_kien.png", bookingImage: "/images/products/5_anh_makeup/05_makeup_su_kien.png" }
];

export const makeupSteps = [
  { title: "Tư vấn phong cách", text: "Trao đổi concept, trang phục, tone da và bối cảnh để chọn layout makeup phù hợp." },
  { title: "Làm sạch và dưỡng da", text: "Làm sạch nhẹ, cân bằng da và dưỡng ẩm để lớp nền bám mịn, hạn chế mốc nền." },
  { title: "Tạo nền", text: "Dùng kem nền, che khuyết điểm và phủ phấn mỏng để giữ hiệu ứng trong trẻo như mẫu." },
  { title: "Tạo khối và má hồng", text: "Tạo khối mềm, nhấn má hồng theo tone đã chọn để khuôn mặt có chiều sâu tự nhiên." },
  { title: "Trang điểm mắt", text: "Định hình chân mày, tán màu mắt, kẻ liner và chuốt mi theo độ sắc nét mong muốn." },
  { title: "Hoàn thiện môi và khóa nền", text: "Chọn màu son, chỉnh tổng thể và xịt khóa nền để lớp makeup bền hơn trong ngày." }
];

export const appointmentRows = [
  ["08:00", "Nguyễn Thị Hòa", "Studio", "Chờ xác nhận"],
  ["10:00", "Trần Mỹ Mai", "Tại nhà", "Đã xác nhận"],
  ["11:00", "Lê Thị Hương", "Studio", "Đã xác nhận"],
  ["15:00", "Phạm Thị Lan", "Tại nhà", "Từ chối"]
];

export const orderRows = [
  ["#DH1024", "Son Black Rouge", "250.000đ", "Đơn mới"],
  ["#DH1025", "Kem nền Perfect Diary", "350.000đ", "Đang giao"],
  ["#DH1026", "Phấn phủ Innisfree", "320.000đ", "Hoàn thành"]
];

export const productRows = [
  ["SP001", "Son Black Rouge", "89", "Đang bán"],
  ["SP002", "Kem nền Perfect Diary", "67", "Đang bán"],
  ["SP003", "Serum The Ordinary", "38", "Sắp hết"]
];
