# 03. Checkout và đơn hàng thật từ giỏ hàng

Status: ready-for-agent
Type: task
Blocked by: 02

## Mục tiêu

Khách hàng checkout các item trong giỏ; backend tạo order thật, tính giá/tồn kho cuối cùng, trừ tồn kho, và admin thấy đơn mới.

## Phạm vi

- Checkout page dùng cart thật.
- Backend order creation hiện có được dùng như nguồn tính giá/tồn kho.
- Clear cart sau khi order thành công.
- Không làm payment gateway thật trong ticket này.

## File dự kiến liên quan

- `frontend/src/components/CheckoutForm.tsx`
- `frontend/src/components/HoanDoanRealPages.tsx`
- `frontend/src/lib/api.ts`
- `backend/Beauty.Api/Services/OrderService.cs`
- `backend/Beauty.Api/Program.cs`
- `backend/Beauty.Api/Models/Requests.cs`
- `backend/Beauty.Api.Tests/`

## Điều kiện hoàn thành

- [ ] Checkout gửi tất cả cart items thật tới `/api/orders`.
- [ ] Backend tính `UnitPrice` và `Total`, không tin giá từ frontend.
- [ ] Backend trừ tồn kho khi order tạo thành công.
- [ ] Frontend hiển thị lỗi khi sản phẩm không tồn tại hoặc tồn kho không đủ.
- [ ] Order mới xuất hiện ở admin order list.
- [ ] Cart được clear sau khi order thành công.

## Test cần có

- [ ] Backend test tạo order nhiều item và trừ tồn kho.
- [ ] Backend test từ chối order khi tồn kho thiếu.
- [ ] Manual verification: cart -> checkout -> admin order list.

## Phụ thuộc task khác

- 02. Giỏ hàng thật với dữ liệu sản phẩm backend.

