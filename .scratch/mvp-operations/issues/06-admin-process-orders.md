# 06. Admin xử lý đơn hàng thật

Status: ready-for-agent
Type: task
Blocked by: 03, 04

## Mục tiêu

Admin theo dõi order items/tổng tiền và đổi trạng thái đơn hàng thật từ giao diện admin.

## Phạm vi

- Admin order list.
- Status actions cho đơn hàng.
- UI feedback và refetch/update sau thao tác.
- Không thêm payment gateway trong ticket này.

## File dự kiến liên quan

- `frontend/src/app/admin/page.tsx`
- `frontend/src/components/AdminViews.tsx`
- `frontend/src/lib/api.ts`
- `backend/Beauty.Api/Program.cs`
- `backend/Beauty.Api/Models/Requests.cs`
- `backend/Beauty.Api.Tests/`

## Điều kiện hoàn thành

- [ ] Admin thấy order items, tổng tiền, thời gian và trạng thái.
- [ ] Admin đổi status order từ UI.
- [ ] Status mới được lưu và hiển thị sau refresh.
- [ ] UI xử lý lỗi auth/network/backend rõ ràng.
- [ ] Không làm thay đổi logic tạo order/trừ tồn kho ngoài phạm vi cần thiết.

## Test cần có

- [ ] Backend test update order status.
- [ ] Backend test update order status cần auth staff/admin.
- [ ] Manual verification: checkout -> admin đổi trạng thái -> refresh.

## Phụ thuộc task khác

- 03. Checkout và đơn hàng thật từ giỏ hàng.
- 04. Auth tối thiểu cho admin.

