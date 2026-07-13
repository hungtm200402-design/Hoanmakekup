# 07. Admin quản lý sản phẩm thật

Status: ready-for-agent
Type: task
Blocked by: 04

## Mục tiêu

Admin tạo, sửa, xóa hoặc ngừng bán sản phẩm thật; dữ liệu mới phản ánh ở shop/customer frontend.

## Phạm vi

- Admin product create/edit/delete hoặc soft-hide theo rule backend.
- Giá, sale price, sale approval, tồn kho, ảnh, slug.
- Product list/detail customer-facing phản ánh thay đổi.
- Không đụng AI content/trusted index.

## File dự kiến liên quan

- `frontend/src/app/admin/page.tsx`
- `frontend/src/components/AdminViews.tsx`
- `frontend/src/lib/api.ts`
- `backend/Beauty.Api/Program.cs`
- `backend/Beauty.Api/Models/Requests.cs`
- `backend/Beauty.Api/Services/OrderService.cs`
- `backend/Beauty.Api.Tests/`

## Điều kiện hoàn thành

- [ ] Admin tạo sản phẩm mới và sản phẩm xuất hiện ở shop.
- [ ] Admin sửa giá/tồn kho/ảnh và customer frontend phản ánh dữ liệu mới.
- [ ] Admin không xóa cứng sản phẩm đã có trong đơn hàng; UI hiển thị hướng xử lý.
- [ ] Validation slug, giá, sale price, tồn kho hiển thị rõ.
- [ ] Không làm hỏng seed/bootstrap product hiện có.

## Test cần có

- [ ] Backend test create/update product validation.
- [ ] Backend test không xóa product đã có order item.
- [ ] Manual verification: tạo/sửa sản phẩm trong admin, kiểm tra shop/detail.

## Phụ thuộc task khác

- 04. Auth tối thiểu cho admin.

