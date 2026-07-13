# Tickets: MVP vận hành thật

Các ticket này xây MVP vận hành thật cho booking, giỏ hàng, checkout, admin operations, auth tối thiểu và availability backend. Source: cuộc trao đổi hiện tại + cấu trúc repo hiện có.

Work the **frontier**: làm ticket không còn blocker trước. Lưu ý: auth tối thiểu là blocker kỹ thuật cho các thao tác admin ghi dữ liệu vì backend hiện đã bảo vệ mutation endpoints bằng `[Authorize]`.

## 01. Booking thật end-to-end

**What to build:** Khách hàng đặt lịch makeup từ frontend, backend tạo appointment thật, admin nhìn thấy lịch mới.

**Blocked by:** None — can start immediately.

- [ ] Form đặt lịch gửi dữ liệu thật tới backend.
- [ ] Backend validate dữ liệu và chống trùng lịch như hiện có.
- [ ] Admin dashboard/list thấy lịch mới sau khi đặt.
- [ ] Không phụ thuộc AI content hoặc trusted product index.

## 02. Giỏ hàng thật với dữ liệu sản phẩm backend

**What to build:** Khách hàng thêm sản phẩm thật vào giỏ, sửa số lượng, xóa sản phẩm, và giỏ được giữ khi refresh.

**Blocked by:** None — can start immediately.

- [ ] Giỏ hàng dùng product id/slug và giá hiển thị từ backend.
- [ ] Số lượng không âm, không bằng 0 trừ khi xóa item.
- [ ] Giỏ hàng persist localStorage cho MVP.
- [ ] Trang giỏ hàng không còn phụ thuộc danh sách sản phẩm hardcoded cho item chính.

## 03. Checkout và đơn hàng thật từ giỏ hàng

**What to build:** Khách hàng checkout các item trong giỏ, backend tạo order thật, trừ tồn kho, admin nhìn thấy đơn mới.

**Blocked by:** 02. Giỏ hàng thật với dữ liệu sản phẩm backend.

- [ ] Checkout gửi toàn bộ cart items thật.
- [ ] Backend là nguồn tính giá/tồn kho cuối cùng.
- [ ] Khi đặt hàng thành công, giỏ hàng được clear.
- [ ] Khi tồn kho thiếu, frontend hiển thị lỗi rõ ràng.

## 04. Auth tối thiểu cho admin

**What to build:** Admin có cách đăng nhập tối thiểu để gọi được các endpoint staff/admin đang được bảo vệ.

**Blocked by:** None — can start immediately.

- [ ] Admin UI có luồng đăng nhập/đăng xuất tối thiểu.
- [ ] Token/role đủ để gọi các endpoint có policy `Staff` và `Admin`.
- [ ] Các trang admin xử lý trạng thái chưa đăng nhập rõ ràng.
- [ ] Không mở public mutation endpoint chỉ để né auth.

## 05. Admin xử lý lịch hẹn thật

**What to build:** Admin xác nhận, từ chối, hoàn thành hoặc hủy lịch hẹn thật từ giao diện admin.

**Blocked by:** 01. Booking thật end-to-end; 04. Auth tối thiểu cho admin.

- [ ] Danh sách lịch hiển thị dữ liệu thật và trạng thái hiện tại.
- [ ] Admin đổi status appointment từ UI.
- [ ] UI refresh hoặc cập nhật row sau thao tác.
- [ ] Trạng thái hợp lệ được test ở backend hoặc qua integration test.

## 06. Admin xử lý đơn hàng thật

**What to build:** Admin đổi trạng thái đơn hàng thật và theo dõi order items/tổng tiền từ giao diện admin.

**Blocked by:** 03. Checkout và đơn hàng thật từ giỏ hàng; 04. Auth tối thiểu cho admin.

- [ ] Danh sách đơn hiển thị order items, tổng tiền, trạng thái.
- [ ] Admin đổi status order từ UI.
- [ ] UI xử lý lỗi auth/network/backend rõ ràng.
- [ ] Test xác nhận status transition ghi vào database.

## 07. Admin quản lý sản phẩm thật

**What to build:** Admin tạo, sửa, xóa hoặc ngừng bán sản phẩm thật; cập nhật giá, sale, tồn kho và ảnh.

**Blocked by:** 04. Auth tối thiểu cho admin.

- [ ] Admin tạo sản phẩm mới và sản phẩm xuất hiện ở shop.
- [ ] Admin sửa giá/tồn kho/ảnh và frontend phản ánh dữ liệu mới.
- [ ] Sản phẩm đã có trong đơn không bị xóa cứng trái rule backend.
- [ ] Test validation slug, giá, sale price và tồn kho.

## 08. Availability lấy từ backend

**What to build:** Trang đặt lịch lấy slot khả dụng từ backend thay vì lịch/giờ hardcoded.

**Blocked by:** 01. Booking thật end-to-end.

- [ ] Backend trả danh sách ngày/giờ khả dụng theo service/date range.
- [ ] Frontend chỉ cho chọn slot khả dụng.
- [ ] Sau khi đặt lịch, slot liên quan không còn khả dụng.
- [ ] Test overlap/availability theo appointment đã có.

