# 01. Booking thật end-to-end

Status: ready-for-agent
Type: task
Blocked by: None

## Mục tiêu

Khách hàng đặt lịch makeup từ frontend, backend tạo appointment thật, và admin nhìn thấy lịch mới trong dashboard/list.

## Phạm vi

- Luồng đặt lịch customer-facing trên trang đặt lịch và/hoặc đăng ký tư vấn.
- Backend tạo appointment với validation hiện có.
- Admin read view hiển thị lịch mới.
- Không thay đổi AI content hoặc trusted product index.

## File dự kiến liên quan

- `frontend/src/components/HoanDoanRealPages.tsx`
- `frontend/src/components/ConsultationBookingPage.tsx`
- `frontend/src/lib/api.ts`
- `backend/Beauty.Api/Program.cs`
- `backend/Beauty.Api/Services/AppointmentService.cs`
- `backend/Beauty.Api/Models/Requests.cs`
- `backend/Beauty.Api.Tests/`

## Điều kiện hoàn thành

- [ ] Submit form booking tạo appointment thật qua `/api/appointments`.
- [ ] Backend trả lỗi rõ khi thiếu tên/số điện thoại/dịch vụ hoặc giờ không hợp lệ.
- [ ] Backend chống đặt trùng khung giờ đang hoạt động.
- [ ] Admin dashboard/list thấy lịch mới sau khi refresh.
- [ ] Luồng này không phụ thuộc Gemini, AI content, hoặc trusted product index.

## Test cần có

- [ ] Backend test tạo appointment thành công.
- [ ] Backend test từ chối appointment bị trùng slot.
- [ ] Frontend/manual verification: đặt lịch từ UI và thấy lịch trong admin.

## Phụ thuộc task khác

Không có.

## Known gaps / technical debt

- Admin appointments read endpoint is intentionally left as a known security gap for ticket 04, where minimal admin auth will be implemented across admin workflows.
- The two frontend booking submit flows duplicate payload/error handling. Keep this as technical debt for a later frontend cleanup so ticket 01 stays focused on booking correctness.
