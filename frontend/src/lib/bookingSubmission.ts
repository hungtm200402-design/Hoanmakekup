export type AppointmentPayload = {
  customerName: FormDataEntryValue | null;
  phone: FormDataEntryValue | null;
  email: string;
  address: string;
  service: FormDataEntryValue | null;
  tone: string | FormDataEntryValue | null;
  note: FormDataEntryValue | null;
  startAt: string;
};

export type AppointmentSubmissionResult =
  | { ok: true }
  | { ok: false; message: string };

export type AvailableAppointmentSlot = { date: string; time: string };

export function resolveBookingApiUrl(path: string, hostname = typeof window === "undefined" ? "" : window.location.hostname) {
  const configuredOrigin = typeof process === "undefined" ? "" : process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "");
  if (configuredOrigin) {
    return `${configuredOrigin}${path}`;
  }

  if (hostname === "localhost" || hostname === "127.0.0.1") {
    return `http://127.0.0.1:5000${path}`;
  }

  return path;
}

export function createBookingSubmissionGate() {
  let inFlight = false;

  return {
    async run<T>(operation: () => Promise<T>) {
      if (inFlight) return undefined;

      inFlight = true;
      try {
        return await operation();
      } finally {
        inFlight = false;
      }
    }
  };
}

export async function submitAppointment(payload: AppointmentPayload): Promise<AppointmentSubmissionResult> {
  let response: Response;
  try {
    response = await fetch(resolveBookingApiUrl("/api/appointments"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
  } catch {
    return { ok: false, message: "Không kết nối được máy chủ đặt lịch. Vui lòng kiểm tra lại kết nối rồi thử lại." };
  }

  if (response.ok) return { ok: true };

  const text = await response.text().catch(() => "");
  return { ok: false, message: readError(text) ?? `Không đặt được lịch. Máy chủ trả mã lỗi ${response.status}.` };
}

export async function fetchAvailableAppointmentSlots(date: string) {
  try {
    const response = await fetch(resolveBookingApiUrl(`/api/appointments/availability?date=${encodeURIComponent(date)}`));
    if (!response.ok) {
      return { ok: false as const, message: "Không tải được giờ trống. Vui lòng thử lại." };
    }

    return { ok: true as const, slots: await response.json() as AvailableAppointmentSlot[] };
  } catch {
    return { ok: false as const, message: "Không kết nối được máy chủ để tải giờ trống." };
  }
}

function readError(text: string) {
  if (!text) return null;

  try {
    const data = JSON.parse(text) as { error?: string; title?: string };
    return data.error ?? data.title ?? null;
  } catch {
    return null;
  }
}
