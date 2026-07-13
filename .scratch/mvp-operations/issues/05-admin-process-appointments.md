# 05. Admin xử lý lịch hẹn thật

Status: ready-for-agent
Type: task
Blocked by: 01, 04

## Mục tiêu

Admin xác nhận, từ chối, hoàn thành hoặc hủy lịch hẹn thật từ giao diện admin.

## Phạm vi

- Admin appointment list.
- Status actions gọi backend endpoint hiện có hoặc endpoint được chỉnh tối thiểu.
- UI feedback khi thao tác thành công/lỗi.
- Không thêm availability trong ticket này.

## File dự kiến liên quan

- `frontend/src/app/admin/page.tsx`
- `frontend/src/components/AdminViews.tsx`
- `frontend/src/lib/api.ts`
- `backend/Beauty.Api/Program.cs`
- `backend/Beauty.Api/Models/Requests.cs`
- `backend/Beauty.Api.Tests/`

## Điều kiện hoàn thành

- [ ] Admin thấy danh sách lịch thật với trạng thái hiện tại.
- [ ] Admin đổi trạng thái appointment từ UI.
- [ ] Row được cập nhật sau thao tác hoặc list được refetch.
- [ ] UI hiển thị lỗi auth/network/backend dễ hiểu.
- [ ] Status mới được lưu trong database.

## Test cần có

- [ ] Backend test update appointment status.
- [ ] Backend test update appointment status cần auth staff/admin.
- [ ] Manual verification: đặt lịch -> admin xác nhận/từ chối -> refresh vẫn giữ trạng thái.

## Phụ thuộc task khác

- 01. Booking thật end-to-end.
- 04. Auth tối thiểu cho admin.

