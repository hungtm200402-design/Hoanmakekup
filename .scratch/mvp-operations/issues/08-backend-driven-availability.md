# 08. Availability lấy từ backend

Status: ready-for-agent
Type: task
Blocked by: 01

## Mục tiêu

Trang đặt lịch lấy slot khả dụng từ backend thay vì dùng lịch/giờ hardcoded.

## Phạm vi

- Backend endpoint trả slot khả dụng theo ngày/date range và có thể theo service nếu cần.
- Frontend booking UI hiển thị slot khả dụng.
- Sau khi đặt lịch thành công, slot bị chiếm không còn khả dụng.
- Không thay đổi logic AI/admin content.

## File dự kiến liên quan

- `frontend/src/components/HoanDoanRealPages.tsx`
- `frontend/src/components/BookingForm.tsx`
- `frontend/src/lib/api.ts`
- `backend/Beauty.Api/Program.cs`
- `backend/Beauty.Api/Services/AppointmentService.cs`
- `backend/Beauty.Api/Models/Requests.cs`
- `backend/Beauty.Api.Tests/`

## Điều kiện hoàn thành

- [ ] Backend trả danh sách slot khả dụng trong khoảng ngày được yêu cầu.
- [ ] Slot đã có appointment chưa bị hủy/từ chối không được trả là khả dụng.
- [ ] Frontend chỉ cho chọn slot backend trả về.
- [ ] Sau khi booking thành công, frontend refresh availability hoặc loại slot vừa đặt.
- [ ] UI vẫn có trạng thái loading/error khi backend không sẵn sàng.

## Test cần có

- [ ] Backend test availability khi không có appointment.
- [ ] Backend test availability loại slot bị overlap.
- [ ] Manual verification: đặt lịch một slot, slot đó biến mất khỏi lựa chọn.

## Phụ thuộc task khác

- 01. Booking thật end-to-end.

