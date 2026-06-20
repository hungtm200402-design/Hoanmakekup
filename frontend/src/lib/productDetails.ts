export const productDetails: Record<string, {
  category: string;
  detailImage?: string;
  bullets: string[];
  optionsTitle: string;
  options: string[];
  description: string;
  usage: string[];
}> = {
  "perfect-diary": {
    category: "Kem nền",
    detailImage: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/07_shop_kem_nen_fit_me_matte.png",
    bullets: ["Lớp nền mỏng nhẹ, dễ tán", "Che phủ tự nhiên, không nặng mặt", "Phù hợp makeup hằng ngày", "Giữ nền mềm mịn trong nhiều giờ"],
    optionsTitle: "Tone màu",
    options: ["N01", "N02", "W01", "W02"],
    description: "Kem nền Perfect Diary cho hiệu ứng nền mịn, sáng nhẹ và phù hợp với phong cách makeup tự nhiên như mẫu.",
    usage: ["Dưỡng ẩm trước khi dùng.", "Lấy lượng nhỏ và tán từ trung tâm mặt ra ngoài.", "Dặm thêm ở vùng cần che phủ.", "Phủ phấn mỏng để khóa nền."]
  },
  "son-black-rouge-air-fit": {
    category: "Son môi",
    detailImage: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/08_shop_son_mac_matte_lipstick.png",
    bullets: ["Chất son mềm mịn, lên màu chuẩn", "Lâu trôi, không gây khô môi", "Bảng màu đa dạng, thời thượng", "Thiết kế sang trọng, tiện lợi"],
    optionsTitle: "Màu sắc",
    options: ["A12 - Đỏ Cam", "A06", "A09", "A18", "A21", "A26"],
    description: "Son Black Rouge Air Fit Velvet Tint tạo sắc đỏ cam tươi tắn, độ blur đẹp và phù hợp dùng hằng ngày.",
    usage: ["Dưỡng môi trước khi thoa.", "Thoa một lớp mỏng lòng môi.", "Tán đều ra viền môi.", "Dặm thêm lớp thứ hai nếu muốn màu rõ hơn."]
  },
  "innisfree-powder": {
    category: "Phấn phủ",
    detailImage: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/09_shop_phan_phu_infallible.png",
    bullets: ["Kiềm dầu nhẹ", "Hạt phấn mịn", "Giữ lớp nền khô thoáng", "Phù hợp dặm lại trong ngày"],
    optionsTitle: "Loại da",
    options: ["Da dầu", "Da hỗn hợp", "Da thường"],
    description: "Phấn phủ Innisfree giúp cố định nền, giảm bóng dầu và giữ bề mặt da mịn hơn.",
    usage: ["Dùng sau kem nền hoặc cushion.", "Lấy phấn bằng cọ hoặc bông phấn.", "Dặm nhẹ vùng chữ T.", "Không miết mạnh để tránh vỡ nền."]
  },
  "the-ordinary-serum": {
    category: "Chăm sóc da",
    detailImage: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/10_shop_serum_niacinamide.png",
    bullets: ["Kết cấu mỏng nhẹ", "Dễ thấm", "Hỗ trợ nền da trước makeup", "Phù hợp routine chăm sóc da"],
    optionsTitle: "Dung tích",
    options: ["30ml", "60ml"],
    description: "Serum The Ordinary hỗ trợ cấp ẩm và chuẩn bị nền da tốt hơn trước khi trang điểm.",
    usage: ["Làm sạch da.", "Nhỏ 2-3 giọt serum.", "Vỗ nhẹ đến khi thấm.", "Dùng kem dưỡng hoặc chống nắng sau đó."]
  },
  "maybelline-mascara": {
    category: "Mascara",
    detailImage: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/11_shop_mascara_dai_cong_mi.png",
    bullets: ["Làm mi rõ nét", "Đầu cọ dễ dùng", "Phù hợp makeup tự nhiên", "Giúp mắt có chiều sâu hơn"],
    optionsTitle: "Màu",
    options: ["Đen", "Nâu đen"],
    description: "Mascara Maybelline giúp hàng mi nổi bật hơn, phù hợp với các layout makeup nhẹ nhàng và dự tiệc.",
    usage: ["Bấm mi trước khi chuốt.", "Chuốt từ chân mi lên ngọn.", "Đợi khô rồi chuốt lớp thứ hai nếu cần.", "Tẩy trang kỹ vùng mắt cuối ngày."]
  },
  "bioderma": {
    category: "Tẩy trang",
    detailImage: "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham/12_shop_nuoc_tay_trang_micellar.png",
    bullets: ["Làm sạch dịu nhẹ", "Phù hợp dùng hằng ngày", "Không cần chà xát mạnh", "Hỗ trợ làm sạch sau makeup"],
    optionsTitle: "Dung tích",
    options: ["250ml", "500ml"],
    description: "Tẩy trang Bioderma giúp làm sạch lớp makeup và bụi bẩn nhẹ nhàng, giữ da thoải mái sau khi dùng.",
    usage: ["Thấm sản phẩm ra bông tẩy trang.", "Đặt lên vùng makeup vài giây.", "Lau nhẹ theo chiều da.", "Rửa lại nếu cần theo routine cá nhân."]
  }
};
