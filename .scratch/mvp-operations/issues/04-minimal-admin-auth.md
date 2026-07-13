# 04. Auth tối thiểu cho admin

Status: ready-for-agent
Type: task
Blocked by: None

## Mục tiêu

Admin có luồng đăng nhập tối thiểu để gọi được các endpoint staff/admin đang được bảo vệ, không mở public mutation endpoint để né auth.

## Phạm vi

- Luồng đăng nhập/đăng xuất admin tối thiểu.
- Cách lưu token phía frontend đủ cho MVP.
- Backend config/validation token hoặc cơ chế dev/admin token phù hợp.
- Không xây full customer account trong ticket này.

## File dự kiến liên quan

- `frontend/src/app/admin/page.tsx`
- `frontend/src/lib/api.ts`
- `backend/Beauty.Api/Program.cs`
- `backend/Beauty.Api/Models/Entities.cs`
- `backend/Beauty.Api/appsettings.example.json`
- `run-backend.ps1`
- `backend/Beauty.Api.Tests/`

## Điều kiện hoàn thành

- [ ] Admin UI phân biệt trạng thái chưa đăng nhập/đã đăng nhập.
- [ ] Requests admin mutation gửi credential/token cần thiết.
- [ ] Endpoint có policy `Staff`/`Admin` gọi được sau đăng nhập hợp lệ.
- [ ] Đăng xuất xóa credential/token phía frontend.
- [ ] Không làm ảnh hưởng public booking, shop, cart, checkout.

## Test cần có

- [ ] Backend test endpoint protected từ chối request không auth.
- [ ] Backend test role admin/staff được phép gọi endpoint phù hợp.
- [ ] Manual verification: login admin, reload, logout.

## Phụ thuộc task khác

Không có.

