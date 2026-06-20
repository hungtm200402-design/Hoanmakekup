# Linh Makeup API

ASP.NET Core API cho website makeup/mỹ phẩm.

## Chức năng chính

- PostgreSQL qua `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Phân quyền `Customer`, `Staff`, `Admin` bằng JWT.
- Đặt lịch makeup và chặn trùng giờ.
- Tạo đơn hàng, backend tự kiểm tra giá và tồn kho.
- Quản lý sản phẩm, đơn hàng, khách hàng/lịch hẹn.
- Giá sale chỉ ở trạng thái chờ duyệt cho đến khi admin approve.
- AI chỉ tạo bản nháp; không tự đổi giá, không kích hoạt sale, không tự đăng bài.
- Upload ảnh khách vào thư mục riêng tư, kiểm tra đuôi file/kích thước.
- Rate limit toàn API và riêng upload/AI.

## Chạy

```powershell
dotnet restore
dotnet build
dotnet run --project backend/Beauty.Api
```

Không đặt API key trong frontend. Nếu dùng AI thật, cấu hình qua secret/env var ở backend.
