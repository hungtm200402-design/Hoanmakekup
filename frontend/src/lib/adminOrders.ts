export type OrderStatus = "Pending" | "Paid" | "Shipping" | "Completed" | "Cancelled";

export type OrderStatusAction = {
  status: OrderStatus;
  label: string;
  tone: "primary" | "neutral" | "danger";
};

export function orderStatusText(status: string) {
  switch (status) {
    case "Pending": return "Chờ xử lý";
    case "Paid": return "Đã thanh toán";
    case "Shipping": return "Đang giao";
    case "Completed": return "Hoàn thành";
    case "Cancelled": return "Đã hủy";
    default: return status || "Không rõ";
  }
}

export function availableOrderStatusActions(status: string): OrderStatusAction[] {
  switch (status) {
    case "Pending":
      return [
        { status: "Paid", label: "Đã thanh toán", tone: "primary" },
        { status: "Cancelled", label: "Hủy đơn", tone: "danger" }
      ];
    case "Paid":
      return [
        { status: "Shipping", label: "Giao hàng", tone: "primary" },
        { status: "Cancelled", label: "Hủy đơn", tone: "danger" }
      ];
    case "Shipping": return [{ status: "Completed", label: "Hoàn thành", tone: "primary" }];
    default: return [];
  }
}
