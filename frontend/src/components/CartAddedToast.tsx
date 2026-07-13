export function CartAddedToast({ productName }: { productName: string }) {
  return (
    <span aria-live="polite" role="status" className="product-cart-toast">
      <span className="product-cart-toast-icon" aria-hidden="true">✓</span>
      <span>
        <strong>Đã thêm vào giỏ</strong>
        <small>{productName}</small>
      </span>
    </span>
  );
}
