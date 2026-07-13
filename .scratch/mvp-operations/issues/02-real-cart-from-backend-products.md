# 02. Giỏ hàng thật với dữ liệu sản phẩm backend

Status: ready-for-agent
Type: task
Blocked by: None

## Mục tiêu

Khách hàng thêm sản phẩm thật vào giỏ, chỉnh số lượng, xóa sản phẩm, và giỏ được giữ lại khi refresh.

## Phạm vi

- Product list/detail add-to-cart.
- Cart state ở frontend, ưu tiên localStorage cho MVP.
- Cart page hiển thị item chính từ dữ liệu sản phẩm backend.
- Chưa xử lý payment; checkout nằm ở task sau.

## File dự kiến liên quan

- `frontend/src/app/shop-my-pham/[slug]/page.tsx`
- `frontend/src/components/HoanDoanRealPages.tsx`
- `frontend/src/components/ProductGrid.tsx`
- `frontend/src/components/ProductCard.tsx`
- `frontend/src/lib/api.ts`
- `frontend/src/lib/data.ts`

## Điều kiện hoàn thành

- [ ] Add-to-cart dùng product id/slug thật, không chỉ link sang trang giỏ.
- [ ] Cart page hiển thị tên, ảnh, giá, số lượng và tổng tiền từ cart thật.
- [ ] Khách hàng tăng/giảm số lượng và xóa item được.
- [ ] Cart persist qua refresh bằng localStorage.
- [ ] Item trong cart không phụ thuộc danh sách cart hardcoded hiện tại.

## Test cần có

- [ ] Frontend unit/component test cho cart reducer/helper nếu có.
- [ ] Manual verification: thêm sản phẩm từ shop/detail, refresh, chỉnh số lượng, xóa item.

## Phụ thuộc task khác

Không có.

