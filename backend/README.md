# Hoàn Makeup API

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
- Chế độ local mặc định dùng SQLite file `App_Data/beauty.db`, nên lịch hẹn/đơn hàng không mất sau khi backend restart. Có thể đổi sang PostgreSQL bằng connection string.

## Chạy

```powershell
dotnet restore backend/Beauty.Api/Beauty.Api.csproj
dotnet build backend/Beauty.Api/Beauty.Api.csproj
dotnet run --project backend/Beauty.Api/Beauty.Api.csproj
```

API chạy ở `http://127.0.0.1:5000`, khớp rewrite `/api/*` trong frontend Next.js.

## Production PostgreSQL và backup

Đặt `UseInMemoryDatabase=false` và `ConnectionStrings__DefaultConnection` bằng connection string PostgreSQL. Khi API khởi động với PostgreSQL, migration trong `Data/Migrations` được áp dụng tự động. Không đặt `UseInMemoryDatabase=true` nếu cần giữ dữ liệu.

Frontend production nên đặt `NEXT_PUBLIC_API_BASE_URL=https://api.example.com` (xem `frontend/.env.local.example`) để các form booking không phụ thuộc host của Next.

Backup database bằng `pg_dump` từ máy/worker có quyền truy cập PostgreSQL, ví dụ:

```powershell
pg_dump --format=custom --file "backup-$(Get-Date -Format yyyyMMdd-HHmmss).dump" "$env:ConnectionStrings__DefaultConnection"
```

`appsettings.json` được ignore để tránh lộ secret. Nếu cần cấu hình rõ ràng, copy:

```powershell
Copy-Item backend/Beauty.Api/appsettings.example.json backend/Beauty.Api/appsettings.json
```

## Endpoint frontend đang dùng

- `GET /api/products`
- `GET /api/products/{slug}`
- `POST /api/orders`
- `GET /api/admin/dashboard`

## Endpoint quản trị/backend đã có

- `GET /api/admin/appointments`
- `PUT /api/admin/appointments/{id}/status`
- `GET /api/admin/orders`
- `PUT /api/admin/orders/{id}/status`
- `GET /api/admin/products`
- `POST /api/admin/products`
- `PUT /api/admin/products/{id}`
- `DELETE /api/admin/products/{id}`
- `POST /api/admin/sales`
- `POST /api/admin/sales/{productId}/approve`
- `GET /api/admin/customers`
- `POST /api/admin/ai-drafts`
- `GET /api/admin/ai-drafts`
- `PUT /api/admin/ai-drafts/{id}/review`
- `POST /api/private/customer-images`

Các route admin nhạy cảm giữ policy JWT `Staff`/`Admin`. Dashboard đang để public vì frontend hiện chưa có màn đăng nhập/token.

Không đặt API key trong frontend. Nếu dùng AI thật, cấu hình qua secret/env var ở backend.
