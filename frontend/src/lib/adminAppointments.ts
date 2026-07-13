export type AppointmentStatus = "Pending" | "Confirmed" | "Rejected" | "Completed" | "Cancelled";

export type AppointmentStatusAction = {
  status: AppointmentStatus;
  label: string;
  tone: "primary" | "neutral" | "danger";
};

export type AppointmentFilters = {
  customerName?: string;
  phone?: string;
  status?: AppointmentStatus | "";
  service?: string;
  fromDate?: string;
  toDate?: string;
};

export function normalizeAppointmentFilters(filters: AppointmentFilters): Record<string, string> {
  const entries = Object.entries(filters)
    .map(([key, value]) => [key, typeof value === "string" ? value.trim() : ""] as const)
    .filter(([, value]) => Boolean(value));
  return Object.fromEntries(entries);
}

export function appointmentStatusText(status: string) {
  switch (status) {
    case "Pending":
      return "Chờ xác nhận";
    case "Confirmed":
      return "Đã xác nhận";
    case "Rejected":
      return "Đã từ chối";
    case "Completed":
      return "Đã hoàn thành";
    case "Cancelled":
      return "Đã hủy";
    default:
      return status || "Không rõ";
  }
}

export function availableAppointmentStatusActions(status: string): AppointmentStatusAction[] {
  switch (status) {
    case "Pending":
      return [
        { status: "Confirmed", label: "Xác nhận", tone: "primary" },
        { status: "Rejected", label: "Từ chối", tone: "danger" },
        { status: "Cancelled", label: "Hủy", tone: "neutral" }
      ];
    case "Confirmed":
      return [
        { status: "Completed", label: "Hoàn thành", tone: "primary" },
        { status: "Cancelled", label: "Hủy", tone: "neutral" }
      ];
    default:
      return [];
  }
}
