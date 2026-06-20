# Linh Makeup Frontend

Next.js + TypeScript + Tailwind CSS implementation theo bộ ảnh đã cung cấp.

## Routes

- `/` Trang chủ
- `/dich-vu-makeup` Trang dịch vụ makeup
- `/dat-lich` Form đặt lịch
- `/shop-my-pham` Trang bán mỹ phẩm
- `/shop-my-pham/son-black-rouge-air-fit` Chi tiết sản phẩm
- `/gio-hang` Giỏ hàng
- `/thanh-toan` Thanh toán
- `/admin` Trang quản trị

## Chạy

```powershell
npm install
npm run build
npm run dev
```

Frontend không chứa API key. Các request `/api/*` được rewrite sang ASP.NET Core backend ở `http://localhost:5000`.
