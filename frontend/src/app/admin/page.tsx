"use client";

import { ChangeEvent, FormEvent, startTransition, useEffect, useMemo, useRef, useState } from "react";
import { AdminView } from "@/components/AdminViews";
import {
  AdminAppointment,
  AdminCustomer,
  AdminOrder,
  AdminProduct,
  ConfirmedProductPayload,
  DashboardData,
  ProductIdentification,
  SaleContentResult,
  createAdminProduct,
  deleteAdminProduct,
  fetchAdminAppointments,
  fetchAdminCustomers,
  fetchAdminOrders,
  fetchAdminProducts,
  fetchDashboard,
  findOfficialProductUrlFromImage,
  formatVnd,
  identifyProductImage,
  loginAdmin,
  logoutAdmin,
  updateAdminAppointmentStatus,
  updateAdminOrderStatus,
  updateAdminProduct,
  verifyOfficialProductUrl,
  writeSaleContent
} from "@/lib/api";
import { readAdminSession, type AdminAuthSession } from "@/lib/adminAuth";
import type { AppointmentStatus } from "@/lib/adminAppointments";
import type { AppointmentFilters } from "@/lib/adminAppointments";
import type { OrderStatus } from "@/lib/adminOrders";

const menuItems = [
  ["⌂", "TỔNG QUAN", "Dashboard"],
  ["♙", "QUẢN LÝ", "Khách hàng"],
  ["▣", "LỊCH HẸN", "Đặt lịch tư vấn"],
  ["▤", "SẢN PHẨM", "Quản lý sản phẩm"],
  ["🪽", "CONTENT", "Viết bài sale tự động"],
  ["☷", "MARKETING", "Chiến dịch & KM"],
  ["▢", "ĐƠN HÀNG", "Quản lý đơn hàng"],
  ["◴", "THỐNG KÊ", "Báo cáo doanh thu"],
  ["⚙", "CÀI ĐẶT", "Hệ thống"]
];

const menuTitles = menuItems.map(([, title]) => title);
const defaultAdminMenu = "TỔNG QUAN";
const adminMenuStorageKey = "hoan-doan-admin-active-menu";

function isAdminMenu(value: string | null): value is string {
  return Boolean(value && menuTitles.includes(value));
}

function getInitialAdminMenu() {
  if (typeof window === "undefined") {
    return defaultAdminMenu;
  }

  const urlMenu = new URLSearchParams(window.location.search).get("tab");
  if (isAdminMenu(urlMenu)) {
    return urlMenu;
  }

  const storedMenu = window.localStorage.getItem(adminMenuStorageKey);
  return isAdminMenu(storedMenu) ? storedMenu : defaultAdminMenu;
}

const steps = [
  ["1", "TẢI ẢNH SẢN PHẨM", "Chọn hình ảnh"],
  ["2", "TÌM URL CHÍNH HÃNG", "Xác minh nguồn"],
  ["3", "TẠO NỘI DUNG", "Viết bài sale"],
  ["4", "HOÀN THIỆN", "Sao chép & sử dụng"]
];

const pageCopies: Record<string, [string, string, string]> = {
  "TỔNG QUAN": ["⌂", "Tổng quan", "Dashboard điều hành tổng thể hoạt động kinh doanh"],
  "QUẢN LÝ": ["♙", "Quản lý khách hàng", "Theo dõi hồ sơ, hạng thành viên và lịch sử tương tác"],
  "LỊCH HẸN": ["▣", "Lịch hẹn", "Quản lý lịch tư vấn và dịch vụ của khách hàng"],
  "SẢN PHẨM": ["▤", "Sản phẩm", "Quản lý toàn bộ sản phẩm, tồn kho và thông tin chi tiết"],
  "CONTENT": ["♕", "Content – Viết bài sale tự động", "Tải ảnh sản phẩm để Hoàn Doãn Beauty & Academy hỗ trợ tạo bài sale chốt đơn"],
  "MARKETING": ["☷", "Marketing", "Quản lý chiến dịch, khuyến mãi và hoạt động marketing"],
  "ĐƠN HÀNG": ["▢", "Đơn hàng", "Quản lý và theo dõi tất cả đơn hàng"],
  "THỐNG KÊ": ["◴", "Thống kê", "Theo dõi hiệu quả hoạt động kinh doanh toàn diện"],
  "CÀI ĐẶT": ["⚙", "Cài đặt", "Quản lý cấu hình hệ thống và tùy chỉnh theo nhu cầu"]
};

const fixedContactInfo = {
  shopName: "Hoàn Doãn Beauty & Academy",
  phone: "0366 672 986",
  address: "Phúc Thọ"
};

const lockedOptionalFields = new Set(["shopName", "phone", "address"]);

const emptyOptionalInfo = {
  officialProductUrl: "",
  price: "",
  salePrice: "",
  gift: "",
  shopName: fixedContactInfo.shopName,
  phone: fixedContactInfo.phone,
  address: fixedContactInfo.address,
  website: "",
  remainingQuantity: ""
};

function hasDisplayValue(value: string) {
  const normalized = value.trim().toLowerCase();
  return Boolean(normalized) &&
    !["n/a", "na", "null", "undefined", "-"].includes(normalized) &&
    !normalized.includes("chưa cập nhật") &&
    !normalized.includes("đang cập nhật");
}

function cleanSaleText(value?: string | null) {
  if (!value) return "";
  return value
    .normalize("NFKD")
    .replace(/[\u0335\u0336]/g, "")
    .replace(/\*\*/g, "")
    .replace(/__/g, "")
    .replace(/~~/g, "")
    .replace(/<\/?(?:strong|b|del|s)>/gi, "")
    .trim();
}

function buildIdentificationSearchQuery(result: ProductIdentification) {
  return Array.from(new Set([
    result.searchQuery,
    result.productName,
    result.brand,
    result.variant,
    result.shade,
    result.finish,
    result.category,
    result.size,
    ...(result.visibleText ?? [])
  ]
    .map(cleanRecognizedField)
    .filter(hasDisplayValue)))
    .join(" ");
}

function formatWorkflowMessage(message: string) {
  if (message.startsWith("Kết quả nhận diện:")) {
    return message.includes("URL trang sản phẩm")
      ? "Đã tìm được URL sản phẩm đáng tin cậy và ghi kết quả nhận diện. Vui lòng kiểm tra thông tin ở khung Kết quả nhận diện."
      : "Đã đọc được thông tin sản phẩm. Vui lòng kiểm tra ở khung Kết quả nhận diện.";
  }

  if (message.startsWith("Chưa lấy được nguồn Google") || message.startsWith("Hoàn Doãn Beauty & Academy đang bị giới hạn")) {
    return "Bài sale đã hoàn thiện từ thông tin sản phẩm đã xác nhận.";
  }

  return message;
}

function hasRecognizableProductIdentity(result: ProductIdentification) {
  const brand = cleanRecognizedField(result.brand);
  if (!brand || isOnlyBrandLogoRecognition(result)) return false;

  const productName = cleanRecognizedField(result.productName);
  const productLine = cleanRecognizedField(result.productLine);
  const visibleText = (result.visibleText ?? [])
    .map(cleanRecognizedField)
    .filter(hasDisplayValue)
    .join(" ");
  const normalizedBrand = normalizeVisibleText(brand);
  const normalizedVisible = normalizeVisibleText(visibleText);
  const hasVisibleBeyondBrand = normalizedVisible
    .split(" ")
    .filter(Boolean)
    .some((word) => word.length >= 3 && !normalizedBrand.includes(word));

  return Boolean(productName || productLine || hasVisibleBeyondBrand);
}

function isOnlyBrandLogoRecognition(result: ProductIdentification) {
  const brand = cleanRecognizedField(result.brand);
  const normalizedBrand = normalizeVisibleText(brand);
  const normalizedFields = normalizeVisibleText([
    result.productName,
    result.variant,
    result.shade,
    result.visibleText.join(" ")
  ].join(" "));
  if (!normalizedBrand || !normalizedFields) return false;

  const allowedLogoWords = new Set([normalizedBrand, "CD", "DIOR", "YSL", "MAC", "SK", "II"]);
  const words = normalizedFields.split(" ").filter(Boolean);
  const productWords = words.filter((word) => !allowedLogoWords.has(word));
  return productWords.length === 0;
}

function removeVietnameseTone(value: string) {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "d")
    .toLowerCase();
}

function cleanRecognizedField(value?: string | null) {
  const clean = cleanSaleText(value);
  const normalized = clean.toLowerCase();
  if (!normalized || ["n/a", "na", "null", "undefined", "-", "none", "unknown", "unclear"].includes(normalized)) return "";
  if (normalized.includes("không rõ") || normalized.includes("khong ro") || normalized.includes("không chắc") || normalized.includes("khong chac")) return "";
  return clean;
}

function normalizeVisibleText(value: string) {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toUpperCase()
    .replace(/[’`]/g, "'")
    .replace(/D\s*['’]?\s*ORO/g, "D ORO")
    .replace(/[^A-Z0-9]+/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

const visibleTextStopWords = new Set([
  "EAU", "DE", "PARFUM", "TOILETTE", "COLOGNE", "ELIXIR", "SPRAY", "NATURAL", "VAPORISATEUR", "VAPORISATEUR",
  "ML", "FL", "OZ", "MADE", "IN", "THE", "AND", "WITH", "FOR", "NEW", "ORIGINAL", "AUTHENTIC", "LIMITED",
  "NET", "WT", "INGREDIENTS", "WARNING", "DISTRIBUTED", "IMPORT", "EXPORT", "BATCH", "LOT", "REF",
  "CD", "LOGO", "MONOGRAM"
]);

function meaningfulVisibleWords(value: string) {
  return normalizeVisibleText(value)
    .split(" ")
    .filter((word) => word.length > 1 && !/^\d+$/.test(word) && !visibleTextStopWords.has(word));
}

function isFieldSupportedByVisibleText(value: string, normalizedVisibleText: string) {
  const words = meaningfulVisibleWords(value);
  if (!words.length || !normalizedVisibleText) return true;
  const supportedCount = words.filter((word) => normalizedVisibleText.includes(word)).length;
  return supportedCount >= Math.max(1, Math.ceil(words.length * 0.65));
}

function isDescriptorLine(value: string) {
  const normalized = normalizeVisibleText(value);
  if (!normalized) return true;
  if (/\b\d{1,3}\s*(ML|FL|OZ)\b/.test(normalized)) return true;
  return /\b(EAU DE PARFUM|EAU DE TOILETTE|VAPORISATEUR|NATURAL SPRAY|INGREDIENTS|MADE IN|BATCH|LOT)\b/.test(normalized);
}

function titleCaseVisibleLine(value: string) {
  return value
    .toLowerCase()
    .replace(/\b([a-zà-ỹ])/gi, (match) => match.toUpperCase())
    .replace(/\bD Oro\b/g, "D'Oro")
    .replace(/\bMl\b/g, "ml")
    .trim();
}

function buildProductNameFromVisibleText(visibleText: string[], fallbackBrand: string) {
  const lines = visibleText
    .map(cleanRecognizedField)
    .filter((line) => hasDisplayValue(line) && !isDescriptorLine(line))
    .slice(0, 5);
  const parts: string[] = [];
  const brand = cleanRecognizedField(fallbackBrand);

  for (const line of lines) {
    const normalized = normalizeVisibleText(line);
    if (brand && normalizeVisibleText(brand) === normalized) {
      if (!parts.some((part) => normalizeVisibleText(part) === normalized)) parts.push(brand);
      continue;
    }

    const words = meaningfulVisibleWords(line);
    if (words.length && !parts.some((part) => normalizeVisibleText(part) === normalized)) {
      parts.push(titleCaseVisibleLine(line));
    }
  }

  if (!parts.length) return "";
  return parts.join(" ").replace(/\s+/g, " ").trim();
}

function correctIdentificationFromVisibleText(result: ProductIdentification): ProductIdentification {
  const visibleText = result.visibleText.map(cleanRecognizedField).filter(hasDisplayValue);
  const normalized = normalizeVisibleText(visibleText.join(" "));
  const next = { ...result, visibleText };
  const sizeMatch = visibleText.join(" ").match(/(\d{1,3})\s*(ml|mL|ML)\b/);
  const correctedSize = sizeMatch ? `${sizeMatch[1]}ml` : result.size;

  if (isOnlyBrandLogoRecognition(next)) {
    return {
      ...next,
      productName: "",
      variant: "",
      shade: "",
      size: correctedSize,
      confidence: Math.min(result.confidence, 60),
      searchQuery: "",
      needsConfirmation: true,
      message: "Ảnh chỉ đọc được thương hiệu/logo, chưa thấy tên dòng hoặc mã màu đủ chắc để tìm URL chính hãng."
    };
  }

  if (/\bGUCCI\b/.test(normalized) && /\bBLOOM\b/.test(normalized) && /\bAMBROSIA\s+D\s*ORO\b/.test(normalized)) {
    return {
      ...next,
      productName: "Gucci Bloom Ambrosia D'Oro",
      brand: "Gucci",
      variant: "Ambrosia D'Oro",
      category: "Nước hoa",
      finish: result.finish || "Eau de Parfum",
      size: correctedSize,
      confidence: Math.max(result.confidence, 99),
      searchQuery: "Gucci Bloom Ambrosia D'Oro nước hoa official",
      needsConfirmation: false,
      message: "Đã tự sửa theo chữ thật trên bao bì: Gucci Bloom Ambrosia D'Oro."
    };
  }

  if (/\bCREED\b/.test(normalized) && /\bABSOLU\s+AVENTUS\b/.test(normalized)) {
    return {
      ...next,
      productName: "Creed Absolu Aventus",
      brand: "Creed",
      variant: "Absolu Aventus",
      category: "Nước hoa",
      size: correctedSize,
      confidence: Math.max(result.confidence, 99),
      searchQuery: "Creed Absolu Aventus nước hoa official",
      needsConfirmation: false,
      message: "Đã tự sửa theo chữ thật trên bao bì: Creed Absolu Aventus."
    };
  }

  if (/\b(LIBRE)\b/.test(normalized) && /\bBERRY\s+CRUSH\b/.test(normalized)) {
    return {
      ...next,
      productName: "Yves Saint Laurent Libre Berry Crush",
      brand: "Yves Saint Laurent",
      variant: "Berry Crush",
      category: "Nước hoa",
      finish: /EAU\s+DE\s+PARFUM/.test(normalized) ? "Eau de Parfum Fruitée" : result.finish,
      size: correctedSize,
      confidence: Math.max(result.confidence, 99),
      searchQuery: "Yves Saint Laurent Libre Berry Crush Eau de Parfum official",
      needsConfirmation: false,
      message: "Đã tự sửa theo chữ thật trên bao bì: YSL Libre Berry Crush."
    };
  }

  if ((/\bDIOR\b/.test(normalized) || /\bCD\b/.test(normalized)) && /\bLIP\s+GLOW\b/.test(normalized)) {
    return {
      ...next,
      productName: "Dior Addict Lip Glow",
      brand: "Dior",
      variant: "Lip Glow",
      category: "Son dưỡng có màu",
      finish: result.finish || "Color reviver balm",
      size: correctedSize,
      confidence: Math.max(result.confidence, 99),
      searchQuery: "Dior Addict Lip Glow color reviver balm official",
      needsConfirmation: false,
      message: "Đã tự sửa theo chữ thật trên bao bì: Dior Addict Lip Glow."
    };
  }

  if (/\bDIOR\s+ADDICT\b/.test(normalized) && (/\bSHINE\s+LIPSTICK\b/.test(normalized) || /\bHYDRATING\s+SHINE\b/.test(normalized) || /\bROUGE\s+BRILLANT\b/.test(normalized))) {
    return {
      ...next,
      productName: "Dior Addict Hydrating Shine Lipstick",
      brand: "Dior",
      variant: "Dior Addict",
      category: "Lipstick",
      finish: result.finish || "Shine",
      size: correctedSize,
      confidence: Math.max(result.confidence, 99),
      searchQuery: "Dior Addict Hydrating Shine Lipstick official",
      needsConfirmation: false,
      message: "Đã tự sửa theo chữ thật trên bao bì: Dior Addict Hydrating Shine Lipstick."
    };
  }

  const productNameSupported = isFieldSupportedByVisibleText(result.productName, normalized);
  const variantSupported = isFieldSupportedByVisibleText(result.variant, normalized);
  const visibleProductName = buildProductNameFromVisibleText(visibleText, result.brand);
  if (visibleText.length && (!productNameSupported || !variantSupported)) {
    return {
      ...next,
      productName: visibleProductName || "",
      variant: variantSupported ? result.variant : "",
      size: correctedSize,
      confidence: Math.min(result.confidence, 70),
      searchQuery: visibleProductName ? `${visibleProductName} ${result.category || ""} official`.trim() : "",
      needsConfirmation: true,
      message: visibleProductName
        ? `AI trả tên không khớp chữ trên bao bì. Đã ưu tiên chữ trên ảnh: ${visibleProductName}.`
        : "AI trả tên không khớp chữ trên bao bì. Vui lòng nhập lại tên đúng trước khi viết bài sale."
    };
  }

  return next;
}

function hasVisibleTextMismatch(productName: string, variant: string, visibleText: string[]) {
  const normalized = normalizeVisibleText(visibleText.map(cleanRecognizedField).filter(hasDisplayValue).join(" "));
  if (!normalized) return false;
  return !isFieldSupportedByVisibleText(productName, normalized) || !isFieldSupportedByVisibleText(variant, normalized);
}

function normalizeContentIcon(value?: string | null) {
  const clean = cleanSaleText(value).replace(/^[:\-_ ]+|[:\-_ ]+$/g, "");
  if (!clean) return "✨";
  const key = clean.toLowerCase();
  const iconMap: Record<string, string> = {
    sparkle: "✨",
    sparkles: "✨",
    star: "✨",
    stars: "✨",
    shine: "✨",
    glow: "✨",
    droplet: "💧",
    drop: "💧",
    water: "💧",
    moisture: "💧",
    hydration: "💧",
    palette: "🎨",
    color: "🎨",
    colour: "🎨",
    shade: "🎨",
    check: "✅",
    checkmark: "✅",
    tick: "✅",
    done: "✅",
    lipstick: "💄",
    lip: "💄",
    makeup: "💄",
    flower: "🌸",
    rose: "🌸",
    floral: "🌸",
    shield: "🛡️",
    protect: "🛡️",
    protection: "🛡️",
    mirror: "🪞",
    bubble: "🫧",
    bubbles: "🫧",
    leaf: "🌿",
    nature: "🌿",
    sun: "☀️",
    moon: "🌙",
    heart: "💗",
    gift: "🎁",
    bag: "🛍️",
    shopping: "🛍️",
    message: "💌",
    mail: "💌"
  };
  return iconMap[key] ?? clean;
}

function formatHighlightItem(item: SaleContentResult["contentBlocks"][number]["items"][number]) {
  const parts = [normalizeContentIcon(item.icon), cleanSaleText(item.benefitTitle)].filter(hasDisplayValue);
  const head = parts.join(" ");
  const text = cleanSaleText(item.text);
  return [head, text].filter(hasDisplayValue).join(" — ");
}

function formatCallToAction(callToAction: SaleContentResult["callToAction"]) {
  return [cleanSaleText(callToAction.icon), cleanSaleText(callToAction.text)].filter(hasDisplayValue).join(" ");
}

function escapeHtml(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

function escapeRtf(value: string) {
  let result = "";
  for (let index = 0; index < value.length; index += 1) {
    const code = value.charCodeAt(index);
    const char = value[index];
    if (char === "\\") result += "\\\\";
    else if (char === "{") result += "\\{";
    else if (char === "}") result += "\\}";
    else if (char === "\n") result += "\\line ";
    else if (code <= 0x7f) result += char;
    else result += `\\u${code > 32767 ? code - 65536 : code}?`;
  }
  return result;
}

function ResultBlock({ title, value }: { title: string; value: string }) {
  if (!hasDisplayValue(value)) return null;

  return (
    <section className="rounded-xl border border-[#ffd1dc] bg-[#fff8f9] p-4">
      <h3 className="text-[14px] font-bold uppercase text-[#e8326c]">{title}</h3>
      <p className="mt-2 whitespace-pre-line text-[14px] leading-6 text-[#3f2427]">{cleanSaleText(value)}</p>
    </section>
  );
}

function SaleContentBlock({ block }: { block: SaleContentResult["contentBlocks"][number] }) {
  if (block.type === "highlights") {
    return (
      <section className="rounded-xl border border-[#ffd1dc] bg-white/80 p-4">
        {hasDisplayValue(block.title) ? <h4 className="font-bold text-[#e8326c]">{cleanSaleText(block.title)}</h4> : null}
        {hasDisplayValue(block.text) ? <p className="mt-2 whitespace-pre-wrap text-[14px] leading-6 text-[#3f2427]">{cleanSaleText(block.text)}</p> : null}
        {block.items.length ? (
          <ul className="mt-3 grid list-none gap-3 p-0 text-[14px] leading-6 text-[#3f2427]">
            {block.items.map((item, index) => (
              <li key={`${item.icon}-${item.benefitTitle}-${index}`} className="grid grid-cols-[28px_1fr] gap-3">
                <span className="grid h-7 w-7 place-items-center rounded-full bg-[#fff0f5] text-[17px]">{normalizeContentIcon(item.icon)}</span>
                <span><strong className="font-bold text-[#7b2f3a]">{cleanSaleText(item.benefitTitle)}</strong>{hasDisplayValue(item.text) ? ` — ${cleanSaleText(item.text)}` : ""}</span>
              </li>
            ))}
          </ul>
        ) : null}
      </section>
    );
  }

  return (
    <section className="rounded-xl border border-[#ffd1dc] bg-white/80 p-4">
      {hasDisplayValue(block.title) ? <h4 className="font-bold text-[#e8326c]">{cleanSaleText(block.title)}</h4> : null}
      {hasDisplayValue(block.text) ? <p className="mt-2 whitespace-pre-wrap text-[14px] leading-6 text-[#3f2427]">{cleanSaleText(block.text)}</p> : null}
      {block.items.length ? (
        <ul className="mt-3 grid list-none gap-3 p-0 text-[14px] leading-6 text-[#3f2427]">
          {block.items.map((item, index) => (
            <li key={`${item.icon}-${item.benefitTitle}-${index}`} className="grid grid-cols-[28px_1fr] gap-3">
              <span className="grid h-7 w-7 place-items-center rounded-full bg-[#fff0f5] text-[17px]">{normalizeContentIcon(item.icon)}</span>
              <span><strong className="font-bold text-[#7b2f3a]">{cleanSaleText(item.benefitTitle)}</strong>{hasDisplayValue(item.text) ? ` — ${cleanSaleText(item.text)}` : ""}</span>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

function ContactBlock({ contact }: { contact: SaleContentResult["contact"] }) {
  const rows = [contact.shopName, contact.phone, contact.address, contact.website].filter(hasDisplayValue);
  if (!rows.length) return null;

  return (
    <section className="rounded-xl border border-[#ffd1dc] bg-[#fff8f9] p-4">
      <h3 className="text-[14px] font-bold uppercase text-[#e8326c]">Thông tin liên hệ</h3>
      <div className="mt-2 grid gap-1 text-[14px] leading-6 text-[#3f2427]">
        {hasDisplayValue(contact.shopName) ? <p>{contact.shopName}</p> : null}
        {hasDisplayValue(contact.phone) ? <p>{contact.phone}</p> : null}
        {hasDisplayValue(contact.address) ? <p>{contact.address}</p> : null}
        {hasDisplayValue(contact.website) ? <p>{contact.website}</p> : null}
      </div>
    </section>
  );
}

function SalePriceBlock({ price, salePrice, remainingQuantity, gift }: { price: string; salePrice: string; remainingQuantity: string; gift: string }) {
  const originalPrice = formatVnd(price);
  const currentPrice = formatVnd(salePrice || price);
  const showOriginalPrice = Boolean(originalPrice && salePrice && currentPrice && originalPrice !== currentPrice);
  const hasPriceBlock = Boolean(currentPrice || hasDisplayValue(remainingQuantity) || hasDisplayValue(gift));

  if (!hasPriceBlock) return null;

  return (
    <section className="rounded-xl border border-[#ffb9ca] bg-[#fff1f5] p-4 shadow-[0_10px_20px_rgba(232,50,108,0.08)]">
      <div className="sale-price-block text-[14px] text-[#3f2427]">
        {showOriginalPrice ? (
          <p className="sale-price-row">
            <span aria-hidden="true">🏷️</span>
            <span>Giá niêm yết:</span>
            <span className="sale-original-price">{originalPrice}</span>
          </p>
        ) : null}
        {currentPrice ? (
          <p className="sale-price-row">
            <span aria-hidden="true">💰</span>
            <span>Giá hiện tại:</span>
            <span className="sale-current-price">{currentPrice}</span>
          </p>
        ) : null}
        {hasDisplayValue(remainingQuantity) ? (
          <p className="sale-price-row">
            <span aria-hidden="true">📦</span>
            <span>Số lượng hiện có:</span>
            <span>{cleanSaleText(remainingQuantity)}</span>
          </p>
        ) : null}
        {hasDisplayValue(gift) ? (
          <p className="sale-price-row">
            <span aria-hidden="true">🎁</span>
            <span>Quà tặng:</span>
            <span>{cleanSaleText(gift)}</span>
          </p>
        ) : null}
      </div>
    </section>
  );
}

function composePriceText(price: string, salePrice: string, remainingQuantity: string, gift: string) {
  const lines: string[] = [];
  const originalPrice = formatVnd(price);
  const currentPrice = formatVnd(salePrice || price);
  if (originalPrice && salePrice && currentPrice && originalPrice !== currentPrice) {
    lines.push(`🏷️ Giá niêm yết: ${originalPrice}`);
  }
  if (currentPrice) {
    lines.push(`💰 Giá hiện tại: ${currentPrice}`);
  }
  if (hasDisplayValue(remainingQuantity)) {
    lines.push(`📦 Số lượng hiện có: ${cleanSaleText(remainingQuantity)}`);
  }
  if (hasDisplayValue(gift)) {
    lines.push(`🎁 Quà tặng: ${cleanSaleText(gift)}`);
  }
  return lines.join("\n");
}

function composePriceHtml(price: string, salePrice: string, remainingQuantity: string, gift: string) {
  const lines: string[] = [];
  const originalPrice = formatVnd(price);
  const currentPrice = formatVnd(salePrice || price);
  if (originalPrice && salePrice && currentPrice && originalPrice !== currentPrice) {
    lines.push(`🏷️ Giá niêm yết: <span style="text-decoration: line-through; text-decoration-thickness: 1.5px;">${escapeHtml(originalPrice)}</span>`);
  }
  if (currentPrice) {
    lines.push(`💰 Giá hiện tại: <strong>${escapeHtml(currentPrice)}</strong>`);
  }
  if (hasDisplayValue(remainingQuantity)) {
    lines.push(`📦 Số lượng hiện có: ${escapeHtml(cleanSaleText(remainingQuantity))}`);
  }
  if (hasDisplayValue(gift)) {
    lines.push(`🎁 Quà tặng: ${escapeHtml(cleanSaleText(gift))}`);
  }
  return lines;
}

function composePriceRtf(price: string, salePrice: string, remainingQuantity: string, gift: string) {
  const lines: string[] = [];
  const originalPrice = formatVnd(price);
  const currentPrice = formatVnd(salePrice || price);
  if (originalPrice && salePrice && currentPrice && originalPrice !== currentPrice) {
    lines.push(`${escapeRtf("🏷️ Giá niêm yết: ")}\\strike ${escapeRtf(originalPrice)}\\strike0`);
  }
  if (currentPrice) {
    lines.push(escapeRtf(`💰 Giá hiện tại: ${currentPrice}`));
  }
  if (hasDisplayValue(remainingQuantity)) {
    lines.push(escapeRtf(`📦 Số lượng hiện có: ${cleanSaleText(remainingQuantity)}`));
  }
  if (hasDisplayValue(gift)) {
    lines.push(escapeRtf(`🎁 Quà tặng: ${cleanSaleText(gift)}`));
  }
  return lines;
}

function composeMainArticle(result: SaleContentResult, optionalInfo: typeof emptyOptionalInfo) {
  const sections: string[] = [];
  const add = (value: string) => {
    const clean = cleanSaleText(value);
    if (clean) sections.push(clean);
  };

  add(result.headline);
  add(result.opening);
  result.contentBlocks.forEach((block) => {
    const lines: string[] = [];
    if (hasDisplayValue(block.title)) lines.push(cleanSaleText(block.title));
    if (hasDisplayValue(block.text)) lines.push(cleanSaleText(block.text));
    if (block.items.length) lines.push(block.items.map(formatHighlightItem).filter(hasDisplayValue).join("\n\n"));
    add(lines.join("\n"));
  });
  add(composePriceText(optionalInfo.price, optionalInfo.salePrice, optionalInfo.remainingQuantity, optionalInfo.gift));
  add(formatCallToAction(result.callToAction));
  add([result.contact.shopName, result.contact.phone, result.contact.address, result.contact.website].filter(hasDisplayValue).map(cleanSaleText).join("\n"));
  if (result.hashtags.length) add(result.hashtags.map(cleanSaleText).join(" "));
  return sections.join("\n\n");
}

function composeMainArticleHtml(result: SaleContentResult, optionalInfo: typeof emptyOptionalInfo) {
  const sections: string[] = [];
  const add = (value: string) => {
    const clean = cleanSaleText(value);
    if (clean) sections.push(`<div>${escapeHtml(clean).replace(/\n/g, "<br>")}</div>`);
  };

  add(result.headline);
  add(result.opening);
  result.contentBlocks.forEach((block) => {
    const lines: string[] = [];
    if (hasDisplayValue(block.title)) lines.push(cleanSaleText(block.title));
    if (hasDisplayValue(block.text)) lines.push(cleanSaleText(block.text));
    if (block.items.length) lines.push(block.items.map(formatHighlightItem).filter(hasDisplayValue).join("\n\n"));
    add(lines.join("\n"));
  });
  const priceLines = composePriceHtml(optionalInfo.price, optionalInfo.salePrice, optionalInfo.remainingQuantity, optionalInfo.gift);
  if (priceLines.length) sections.push(`<div>${priceLines.join("<br>")}</div>`);
  add(formatCallToAction(result.callToAction));
  add([result.contact.shopName, result.contact.phone, result.contact.address, result.contact.website].filter(hasDisplayValue).map(cleanSaleText).join("\n"));
  if (result.hashtags.length) add(result.hashtags.map(cleanSaleText).join(" "));
  return `<div style="line-height:1.6;">${sections.join("<br><br>")}</div>`;
}

function composeMainArticleRtf(result: SaleContentResult, optionalInfo: typeof emptyOptionalInfo) {
  const sections: string[] = [];
  const add = (value: string) => {
    const clean = cleanSaleText(value);
    if (clean) sections.push(escapeRtf(clean));
  };

  add(result.headline);
  add(result.opening);
  result.contentBlocks.forEach((block) => {
    const lines: string[] = [];
    if (hasDisplayValue(block.title)) lines.push(cleanSaleText(block.title));
    if (hasDisplayValue(block.text)) lines.push(cleanSaleText(block.text));
    if (block.items.length) lines.push(block.items.map(formatHighlightItem).filter(hasDisplayValue).join("\n\n"));
    add(lines.join("\n"));
  });
  const priceLines = composePriceRtf(optionalInfo.price, optionalInfo.salePrice, optionalInfo.remainingQuantity, optionalInfo.gift);
  if (priceLines.length) sections.push(priceLines.join("\\line "));
  add(formatCallToAction(result.callToAction));
  add([result.contact.shopName, result.contact.phone, result.contact.address, result.contact.website].filter(hasDisplayValue).map(cleanSaleText).join("\n"));
  if (result.hashtags.length) add(result.hashtags.map(cleanSaleText).join(" "));
  return `{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 Arial;}}\\f0\\fs24 ${sections.join("\\line\\line ")}}`;
}

export default function AdminPage() {
  const [activeMenu, setActiveMenu] = useState(defaultAdminMenu);
  const [menuReady, setMenuReady] = useState(false);
  const [dashboard, setDashboard] = useState<DashboardData | null>(null);
  const [appointments, setAppointments] = useState<AdminAppointment[]>([]);
  const [orders, setOrders] = useState<AdminOrder[]>([]);
  const [products, setProducts] = useState<AdminProduct[]>([]);
  const [customers, setCustomers] = useState<AdminCustomer[]>([]);
  const [error, setError] = useState("");
  const [adminSession, setAdminSession] = useState<AdminAuthSession | null>(null);
  const [authReady, setAuthReady] = useState(false);
  const [loginMessage, setLoginMessage] = useState("");
  const [loggingIn, setLoggingIn] = useState(false);
  const [appointmentActionMessage, setAppointmentActionMessage] = useState("");
  const [updatingAppointmentId, setUpdatingAppointmentId] = useState("");
  const [orderActionMessage, setOrderActionMessage] = useState("");
  const [updatingOrderId, setUpdatingOrderId] = useState("");
  const [productActionMessage, setProductActionMessage] = useState("");
  const [updatingProductId, setUpdatingProductId] = useState("");
  const [loadingAdminData, setLoadingAdminData] = useState(false);
  const appointmentRequestRef = useRef<AbortController | null>(null);
  const appointmentLoadVersionRef = useRef(0);
  const [previewImage, setPreviewImage] = useState("");
  const [productImageFile, setProductImageFile] = useState<File | null>(null);
  const productImageInputRef = useRef<HTMLInputElement | null>(null);
  const aiImageRunIdRef = useRef(0);
  const identifyAbortRef = useRef<AbortController | null>(null);
  const writeAbortRef = useRef<AbortController | null>(null);
  const [aiMessage, setAiMessage] = useState("");
  const [identifying, setIdentifying] = useState(false);
  const [findingOfficialUrl, setFindingOfficialUrl] = useState(false);
  const [writing, setWriting] = useState(false);
  const [identification, setIdentification] = useState<ProductIdentification | null>(null);
  const [pendingIdentification, setPendingIdentification] = useState<ProductIdentification | null>(null);
  const [saleResult, setSaleResult] = useState<SaleContentResult | null>(null);
  const [verifiedOfficialUrl, setVerifiedOfficialUrl] = useState("");
  const [confirmedName, setConfirmedName] = useState("");
  const [confirmedBrand, setConfirmedBrand] = useState("");
  const [confirmedVariant, setConfirmedVariant] = useState("");
  const [confirmedShade, setConfirmedShade] = useState("");
  const [confirmedFinish, setConfirmedFinish] = useState("");
  const [confirmedCategory, setConfirmedCategory] = useState("");
  const [confirmedSize, setConfirmedSize] = useState("");
  const [optionalInfo, setOptionalInfo] = useState(emptyOptionalInfo);

  useEffect(() => {
    startTransition(() => {
      setActiveMenu(getInitialAdminMenu());
      setMenuReady(true);
      setAdminSession(readAdminSession());
      setAuthReady(true);
    });
  }, []);

  useEffect(() => {
    if (!menuReady) {
      return;
    }

    const params = new URLSearchParams(window.location.search);
    params.set("tab", activeMenu);
    const nextUrl = `${window.location.pathname}?${params.toString()}${window.location.hash}`;
    window.history.replaceState(null, "", nextUrl);
    window.localStorage.setItem(adminMenuStorageKey, activeMenu);
  }, [activeMenu, menuReady]);

  useEffect(() => {
    function restoreMenuFromHistory() {
      const urlMenu = new URLSearchParams(window.location.search).get("tab");
      if (isAdminMenu(urlMenu)) {
        setActiveMenu(urlMenu);
      }
    }

    window.addEventListener("popstate", restoreMenuFromHistory);
    return () => window.removeEventListener("popstate", restoreMenuFromHistory);
  }, []);

  useEffect(() => {
    if (!adminSession) {
      return;
    }

    loadAdminData();
  }, [adminSession]);

  const mainArticleText = useMemo(() => saleResult ? composeMainArticle(saleResult, optionalInfo) : "", [optionalInfo, saleResult]);
  const mainArticleHtml = useMemo(() => saleResult ? composeMainArticleHtml(saleResult, optionalInfo) : "", [optionalInfo, saleResult]);
  const mainArticleRtf = useMemo(() => saleResult ? composeMainArticleRtf(saleResult, optionalInfo) : "", [optionalInfo, saleResult]);
  const wordCount = useMemo(() => mainArticleText.trim().split(/\s+/).filter(Boolean).length, [mainArticleText]);
  const [pageIcon, pageTitle, pageSubtitle] = pageCopies[activeMenu] ?? pageCopies["CONTENT"];
  const hasOcrMismatch = Boolean(identification && hasVisibleTextMismatch(confirmedName, confirmedVariant, identification.visibleText));
  const currentOfficialUrl = optionalInfo.officialProductUrl.trim();
  const officialUrlMatchesCurrentProduct = Boolean(currentOfficialUrl && verifiedOfficialUrl && currentOfficialUrl === verifiedOfficialUrl);
  const hasOfficialProductUrl = Boolean(currentOfficialUrl && verifiedOfficialUrl && currentOfficialUrl === verifiedOfficialUrl);
  const canWrite = Boolean(identification && confirmedName.trim() && hasOfficialProductUrl && !hasOcrMismatch);
  const activeStepIndex = useMemo(() => {
    if (saleResult?.researchSuccessful) return 3;
    if (writing) return 2;
    if (identification) return 2;
    if (pendingIdentification) return 1;
    if (identifying || findingOfficialUrl) return 1;
    return productImageFile ? 0 : 0;
  }, [findingOfficialUrl, identification, identifying, pendingIdentification, productImageFile, saleResult, writing]);
  const workflowStatus = useMemo(() => {
    if (aiMessage) return formatWorkflowMessage(aiMessage);
    if (writing) return hasOfficialProductUrl
      ? "Hoàn Doãn Beauty & Academy đang đọc link sản phẩm chính thức và viết bài sale."
      : "Hoàn Doãn Beauty & Academy đang viết bài sale từ thông tin sản phẩm đã xác nhận.";
    if (findingOfficialUrl) return "Hoàn Doãn Beauty & Academy đang tìm và xác minh URL trang sản phẩm đáng tin cậy.";
    if (identifying) return "Hoàn Doãn Beauty & Academy đang đọc ảnh để lấy từ khóa tìm URL sản phẩm trước.";
    if (saleResult?.researchSuccessful) return "Bài sale đã sẵn sàng. Bạn có thể sao chép bài viết hoặc caption để sử dụng.";
    if (identification && currentOfficialUrl && !officialUrlMatchesCurrentProduct) return "URL hiện tại chưa được backend xác minh nên chưa dùng để viết bài sale.";
    if (identification && !hasOfficialProductUrl) return "Thông tin sản phẩm đã được nhận diện, nhưng chưa có URL được backend xác minh nên chưa thể viết bài sale.";
    if (identification) return "Đã tìm được URL sản phẩm đáng tin cậy và ghi kết quả nhận diện. Bạn kiểm tra lại rồi bấm tạo bài sale.";
    if (pendingIdentification) return "Đã đọc được ảnh nhưng cần xác nhận thêm tên dòng hoặc loại vỏ/lõi/sản phẩm hoàn chỉnh trước khi ghi kết quả.";
    if (productImageFile) return "Ảnh sản phẩm đã sẵn sàng. Bấm tìm URL & nhận diện để bắt đầu.";
    return "Tải ảnh sản phẩm để Hoàn Doãn Beauty & Academy tìm URL sản phẩm đáng tin cậy trước khi ghi kết quả.";
  }, [aiMessage, currentOfficialUrl, findingOfficialUrl, hasOfficialProductUrl, identification, identifying, officialUrlMatchesCurrentProduct, pendingIdentification, productImageFile, saleResult, writing]);

  useEffect(() => {
    if (activeMenu !== "CONTENT") {
      return;
    }

    function pasteProductImage(event: ClipboardEvent) {
      const items = Array.from(event.clipboardData?.items ?? []);
      const imageItem = items.find((item) => item.kind === "file" && item.type.startsWith("image/"));
      const file = imageItem?.getAsFile();
      if (!file) {
        return;
      }

      event.preventDefault();
      applyProductImage(file, "paste");
    }

    window.addEventListener("paste", pasteProductImage);
    return () => window.removeEventListener("paste", pasteProductImage);
  }, [activeMenu]);

  function applyProductImage(file: File, source: "upload" | "paste") {
    const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
    if (!allowedTypes.includes(file.type)) {
      setAiMessage("Ảnh chưa đúng định dạng. Vui lòng dùng JPG, PNG hoặc WebP.");
      return;
    }

    aiImageRunIdRef.current += 1;
    identifyAbortRef.current?.abort();
    writeAbortRef.current?.abort();
    setPreviewImage(URL.createObjectURL(file));
    setProductImageFile(file);
    setIdentifying(false);
    setFindingOfficialUrl(false);
    setWriting(false);
    clearAiResult(false);
    setAiMessage(source === "paste"
      ? "Đã dán ảnh sản phẩm. Bấm “Tìm URL & nhận diện” để Hoàn Doãn Beauty & Academy tìm link sản phẩm trước."
      : "Ảnh đã tải lên. Bấm “Tìm URL & nhận diện” để Hoàn Doãn Beauty & Academy tìm link sản phẩm trước.");
  }

  function uploadImage(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    applyProductImage(file, "upload");
    event.target.value = "";
  }

  function clearAiResult(clearImage = true) {
    aiImageRunIdRef.current += 1;
    identifyAbortRef.current?.abort();
    writeAbortRef.current?.abort();
    if (clearImage) {
      setPreviewImage("");
      setProductImageFile(null);
    }
    setIdentification(null);
    setPendingIdentification(null);
    setSaleResult(null);
    setVerifiedOfficialUrl("");
    setConfirmedName("");
    setConfirmedBrand("");
    setConfirmedVariant("");
    setConfirmedShade("");
    setConfirmedFinish("");
    setConfirmedCategory("");
    setConfirmedSize("");
    setOptionalInfo(emptyOptionalInfo);
    setAiMessage("");
  }

  async function identifyProduct() {
    if (identifying || findingOfficialUrl) {
      return;
    }

    const imageFile = productImageFile;
    if (!imageFile) {
      setAiMessage("Vui lòng tải ảnh sản phẩm trước khi nhận diện.");
      return;
    }

    const runId = aiImageRunIdRef.current + 1;
    aiImageRunIdRef.current = runId;
    identifyAbortRef.current?.abort();
    const controller = new AbortController();
    identifyAbortRef.current = controller;
    setIdentifying(true);
    setFindingOfficialUrl(true);
    setIdentification(null);
    setPendingIdentification(null);
    setSaleResult(null);
    setConfirmedName("");
    setConfirmedBrand("");
    setConfirmedVariant("");
    setConfirmedShade("");
    setConfirmedFinish("");
    setConfirmedCategory("");
    setConfirmedSize("");
    setOptionalInfo((current) => ({ ...current, officialProductUrl: "" }));
    setVerifiedOfficialUrl("");
    setAiMessage("Đang tìm sản phẩm bằng ảnh...");

    try {
      const visualPayload: ConfirmedProductPayload = {
        productName: "",
        brand: "",
        productLine: "",
        variant: "",
        shade: "",
        finish: "",
        category: "",
        itemForm: "",
        size: "",
        searchQuery: "",
        userConfirmed: false,
        officialProductUrl: "",
        price: optionalInfo.price,
        salePrice: optionalInfo.salePrice,
        gift: optionalInfo.gift,
        shopName: optionalInfo.shopName,
        phone: optionalInfo.phone,
        address: optionalInfo.address,
        website: optionalInfo.website,
        remainingQuantity: optionalInfo.remainingQuantity,
        isRewrite: false,
        previousCreativeDirection: ""
      };
      const officialUrlResult = await findOfficialProductUrlFromImage(visualPayload, imageFile, undefined, controller.signal);
      if (aiImageRunIdRef.current !== runId) {
        console.info("[ai-content] Bỏ qua kết quả visual search cũ vì ảnh đã thay đổi.");
        return;
      }

      if (officialUrlResult.url && officialUrlResult.identification) {
        const result = correctIdentificationFromVisibleText(officialUrlResult.identification);
        fillIdentificationFields(result);
          setPendingIdentification(null);
          setOptionalInfo((current) => ({ ...current, officialProductUrl: officialUrlResult.url }));
          setVerifiedOfficialUrl(officialUrlResult.url);
        setAiMessage(officialUrlResult.message || "Đã nhận diện sản phẩm. Sẵn sàng viết bài.");
        return;
      }

      if (officialUrlResult.identification) {
        const result = correctIdentificationFromVisibleText(officialUrlResult.identification);
        fillIdentificationFields(result);
      }
      setAiMessage(officialUrlResult.message || "Ảnh này chưa được lưu nguồn. Hãy mở trang sản phẩm và bấm Lưu vào Hoàn Doãn hoặc dán URL.");
    } catch (exception) {
      if (aiImageRunIdRef.current !== runId) {
        console.info("[ai-content] Bỏ qua lỗi nhận diện cũ vì ảnh đã thay đổi.");
        return;
      }

      setAiMessage(exception instanceof Error ? exception.message : "Không nhận diện được sản phẩm từ ảnh.");
    } finally {
      if (aiImageRunIdRef.current === runId) {
        setIdentifying(false);
        setFindingOfficialUrl(false);
        if (identifyAbortRef.current === controller) {
          identifyAbortRef.current = null;
        }
      }
    }
  }

  function fillIdentificationFields(result: ProductIdentification) {
    setIdentification(result);
    setPendingIdentification(null);
    setConfirmedName(cleanRecognizedField(result.productName));
    setConfirmedBrand(cleanRecognizedField(result.brand));
    setConfirmedVariant(cleanRecognizedField(result.variant));
    setConfirmedShade(cleanRecognizedField(result.shade));
    setConfirmedFinish(cleanRecognizedField(result.finish));
    setConfirmedCategory(cleanRecognizedField(result.category));
    setConfirmedSize(cleanRecognizedField(result.size));
  }

  async function confirmPendingOfficialUrl() {
    if (!pendingIdentification) {
      setAiMessage("Chưa có dữ liệu nhận diện tạm để ghi kết quả.");
      return;
    }

    const officialUrl = optionalInfo.officialProductUrl.trim();
    if (!officialUrl) {
      setAiMessage("Vui lòng dán URL trang sản phẩm đáng tin cậy trước khi ghi kết quả.");
      return;
    }

    if (!/^https?:\/\/[^ ]+\.[^ ]+/i.test(officialUrl)) {
      setAiMessage("URL sản phẩm chưa đúng định dạng. Vui lòng dán link bắt đầu bằng http:// hoặc https://.");
      return;
    }

    if (findingOfficialUrl || identifying) {
      return;
    }

    identifyAbortRef.current?.abort();
    const controller = new AbortController();
    identifyAbortRef.current = controller;
    setFindingOfficialUrl(true);
    setAiMessage("Backend đang xác minh URL bạn nhập có đúng sản phẩm trong ảnh không...");

    try {
      const verified = await verifyOfficialProductUrl(buildPayloadFromIdentification(pendingIdentification), officialUrl, controller.signal);
      if (!verified.url) {
        setVerifiedOfficialUrl("");
        setAiMessage(verified.message || "URL vừa dán chưa được backend xác minh đúng sản phẩm.");
        return;
      }

      fillIdentificationFields(pendingIdentification);
      setOptionalInfo((current) => ({ ...current, officialProductUrl: verified.url }));
      setVerifiedOfficialUrl(verified.url);
      setAiMessage("Backend đã xác minh URL nhập tay đúng sản phẩm. Vui lòng kiểm tra thông tin ở khung Kết quả nhận diện.");
    } catch (exception) {
      setVerifiedOfficialUrl("");
      setAiMessage(exception instanceof Error ? exception.message : "Không xác minh được URL sản phẩm.");
    } finally {
      setFindingOfficialUrl(false);
      if (identifyAbortRef.current === controller) {
        identifyAbortRef.current = null;
      }
    }
  }

  function buildPayload(isRewrite = false): ConfirmedProductPayload {
    return {
      productName: confirmedName,
      brand: confirmedBrand,
      productLine: identification?.productLine ?? "",
      variant: confirmedVariant,
      shade: confirmedShade,
      finish: confirmedFinish,
      category: confirmedCategory,
      itemForm: identification?.itemForm ?? "",
      size: confirmedSize,
      searchQuery: identification ? buildIdentificationSearchQuery(identification) : "",
      userConfirmed: Boolean(identification && !identification.needsConfirmation && confirmedName.trim()),
      isRewrite,
      previousCreativeDirection: saleResult?.creativeDirection ?? "",
      ...optionalInfo
    };
  }

  function buildPayloadFromIdentification(result: ProductIdentification): ConfirmedProductPayload {
    return {
      productName: cleanRecognizedField(result.productName),
      brand: cleanRecognizedField(result.brand),
      productLine: cleanRecognizedField(result.productLine),
      variant: cleanRecognizedField(result.variant),
      shade: cleanRecognizedField(result.shade),
      finish: cleanRecognizedField(result.finish),
      category: cleanRecognizedField(result.category),
      itemForm: cleanRecognizedField(result.itemForm),
      size: cleanRecognizedField(result.size),
      searchQuery: buildIdentificationSearchQuery(result),
      userConfirmed: false,
      officialProductUrl: "",
      price: optionalInfo.price,
      salePrice: optionalInfo.salePrice,
      gift: optionalInfo.gift,
      shopName: optionalInfo.shopName,
      phone: optionalInfo.phone,
      address: optionalInfo.address,
      website: optionalInfo.website,
      remainingQuantity: optionalInfo.remainingQuantity,
      isRewrite: false,
      previousCreativeDirection: ""
    };
  }

  async function writeArticle(isRewrite = false) {
    if (writing) {
      return;
    }

    if (!identification) {
      setAiMessage("Vui lòng nhận diện sản phẩm trước.");
      return;
    }

    if (!confirmedName.trim()) {
      setAiMessage("Vui lòng xác nhận hoặc nhập tên sản phẩm trước khi tìm hiểu.");
      return;
    }

    if (identification && hasVisibleTextMismatch(confirmedName, confirmedVariant, identification.visibleText)) {
      setAiMessage("Tên hoặc phiên bản hiện tại không khớp chữ trên bao bì. Vui lòng sửa đúng theo chữ nhìn thấy rồi mới viết bài sale.");
      return;
    }

    if (!hasOfficialProductUrl) {
      setAiMessage("Chưa có URL đã được backend xác minh nên chưa thể viết bài sale. Vui lòng tìm URL hoặc dán URL sản phẩm để xác minh trước.");
      return;
    }

    writeAbortRef.current?.abort();
    const controller = new AbortController();
    writeAbortRef.current = controller;
    setWriting(true);
    setAiMessage(isRewrite
      ? "Hoàn Doãn Beauty & Academy đang dùng chính URL đã xác minh để viết lại bài sale theo hướng mới..."
      : "Hoàn Doãn Beauty & Academy đang dùng chính URL đã xác minh để viết bài sale...");

    try {
      const result = await writeSaleContent(buildPayload(isRewrite), controller.signal);
      setSaleResult(result);
      setAiMessage(result.warningMessage || (result.researchSuccessful ? "Bài sale đã hoàn thiện." : result.warningMessage));
    } catch (exception) {
      setAiMessage(exception instanceof Error ? exception.message : "Không viết được bài sale. Vui lòng thử lại.");
    } finally {
      setWriting(false);
      if (writeAbortRef.current === controller) {
        writeAbortRef.current = null;
      }
    }
  }

  async function copyText(value: string, htmlValue = "", rtfValue = "") {
    if (!value.trim()) {
      setAiMessage("Chưa có nội dung để sao chép.");
      return;
    }

    try {
      if (htmlValue && navigator.clipboard?.write && typeof ClipboardItem !== "undefined") {
        const richTypes: Record<string, Blob> = {
          "text/plain": new Blob([value], { type: "text/plain" }),
          "text/html": new Blob([htmlValue], { type: "text/html" })
        };
        if (rtfValue) {
          richTypes["text/rtf"] = new Blob([rtfValue], { type: "text/rtf" });
        }

        try {
          await navigator.clipboard.write([new ClipboardItem(richTypes)]);
        } catch {
          await navigator.clipboard.write([
            new ClipboardItem({
              "text/plain": new Blob([value], { type: "text/plain" }),
              "text/html": new Blob([htmlValue], { type: "text/html" })
            })
          ]);
        }
      } else if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(value);
      } else {
        const textarea = document.createElement("textarea");
        textarea.value = value;
        textarea.style.position = "fixed";
        textarea.style.opacity = "0";
        document.body.appendChild(textarea);
        textarea.focus();
        textarea.select();
        document.execCommand("copy");
        document.body.removeChild(textarea);
      }

      setAiMessage("Đã sao chép nội dung. Bạn có thể dán bài viết vào kênh bán hàng.");
    } catch {
      setAiMessage("Không sao chép được nội dung. Vui lòng thử lại.");
    }
  }

  async function loadAdminData() {
    setLoadingAdminData(true);
    const appointmentLoadVersion = ++appointmentLoadVersionRef.current;
    const results = await Promise.allSettled([
      fetchDashboard(),
      fetchAdminAppointments(),
      fetchAdminOrders(),
      fetchAdminProducts(),
      fetchAdminCustomers()
    ]);
    const [dashboardResult, appointmentsResult, ordersResult, productsResult, customersResult] = results;

    if (dashboardResult.status === "fulfilled") setDashboard(dashboardResult.value);
    if (appointmentsResult.status === "fulfilled" && appointmentLoadVersion === appointmentLoadVersionRef.current) setAppointments(appointmentsResult.value);
    if (ordersResult.status === "fulfilled") setOrders(ordersResult.value);
    if (productsResult.status === "fulfilled") setProducts(productsResult.value);
    if (customersResult.status === "fulfilled") setCustomers(customersResult.value);

    if (results.some((result) => result.status === "rejected")) {
      const authFailed = results.some((result) =>
        result.status === "rejected" &&
        result.reason instanceof Error &&
        result.reason.message.includes("đăng nhập lại"));
      if (authFailed) {
        setAdminSession(null);
        setLoginMessage("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        setDashboard(null);
        setAppointments([]);
        setOrders([]);
        setProducts([]);
        setCustomers([]);
        setLoadingAdminData(false);
        return;
      }

      setError("Một số dữ liệu chưa tải được. Hãy chắc chắn backend đang chạy ở cổng 5000 và token admin còn hiệu lực.");
    } else {
      setError("");
    }
    setLoadingAdminData(false);
  }

  async function changeAppointmentStatus(id: string, status: AppointmentStatus) {
    if (updatingAppointmentId) {
      return;
    }

    setUpdatingAppointmentId(id);
    setAppointmentActionMessage("");
    setError("");

    try {
      const updated = await updateAdminAppointmentStatus(id, status);
      setAppointments((current) => current.map((item) => item.id === updated.id ? updated : item));
      setAppointmentActionMessage(`Đã cập nhật lịch hẹn của ${updated.customerName}.`);
      const refreshedDashboard = await fetchDashboard();
      setDashboard(refreshedDashboard);
    } catch (exception) {
      const message = exception instanceof Error ? exception.message : "Không cập nhật được lịch hẹn. Vui lòng thử lại.";
      if (message.includes("đăng nhập lại")) {
        setAdminSession(null);
        setLoginMessage("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
      } else {
        setAppointmentActionMessage(message);
      }
    } finally {
      setUpdatingAppointmentId("");
    }
  }

  async function changeAppointmentFilters(filters: AppointmentFilters) {
    ++appointmentLoadVersionRef.current;
    appointmentRequestRef.current?.abort();
    const controller = new AbortController();
    appointmentRequestRef.current = controller;
    setLoadingAdminData(true);
    setAppointmentActionMessage("");

    try {
      const filtered = await fetchAdminAppointments(filters, controller.signal);
      if (appointmentRequestRef.current === controller) {
        setAppointments(filtered);
      }
    } catch (exception) {
      if (controller.signal.aborted) return;
      const message = exception instanceof Error ? exception.message : "Không tải được lịch hẹn. Vui lòng thử lại.";
      if (message.includes("đăng nhập lại")) {
        setAdminSession(null);
        setLoginMessage("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
      } else {
        setAppointmentActionMessage(message);
      }
    } finally {
      if (appointmentRequestRef.current === controller) {
        setLoadingAdminData(false);
      }
    }
  }

  async function changeOrderStatus(id: string, status: OrderStatus) {
    if (updatingOrderId) return;
    setUpdatingOrderId(id);
    setOrderActionMessage("");
    try {
      const updated = await updateAdminOrderStatus(id, status);
      setOrders((current) => current.map((item) => item.id === updated.id ? updated : item));
      setOrderActionMessage(`Đã cập nhật đơn ${updated.id.slice(0, 8).toUpperCase()}.`);
      setDashboard(await fetchDashboard());
    } catch (exception) {
      const message = exception instanceof Error ? exception.message : "Không cập nhật được đơn hàng.";
      if (message.includes("đăng nhập lại")) {
        setAdminSession(null);
        setLoginMessage("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
      } else setOrderActionMessage(message);
    } finally {
      setUpdatingOrderId("");
    }
  }

  async function saveProduct(product: AdminProduct | null, payload: Parameters<typeof createAdminProduct>[0]) {
    if (updatingProductId) return false;
    setUpdatingProductId(product?.id ?? "new");
    setProductActionMessage("");
    try {
      const saved = product
        ? await updateAdminProduct(product.id, payload)
        : await createAdminProduct(payload);
      setProducts((current) => {
        const next = product ? current.map((item) => item.id === saved.id ? saved : item) : [...current, saved];
        return next.slice().sort((left, right) => left.name.localeCompare(right.name, "vi"));
      });
      setProductActionMessage(product ? `Đã cập nhật ${saved.name}.` : `Đã tạo ${saved.name}.`);
      return true;
    } catch (exception) {
      const message = exception instanceof Error ? exception.message : "Không lưu được sản phẩm.";
      if (message.includes("đăng nhập lại")) {
        setAdminSession(null);
        setLoginMessage("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
      } else setProductActionMessage(message);
      return false;
    } finally {
      setUpdatingProductId("");
    }
  }

  async function removeProduct(product: AdminProduct) {
    if (updatingProductId) return;
    setUpdatingProductId(product.id);
    setProductActionMessage("");
    try {
      await deleteAdminProduct(product.id);
      setProducts((current) => current.filter((item) => item.id !== product.id));
      setProductActionMessage(`Đã xoá ${product.name}.`);
    } catch (exception) {
      const message = exception instanceof Error ? exception.message : "Không xoá được sản phẩm.";
      if (message.includes("đăng nhập lại")) {
        setAdminSession(null);
        setLoginMessage("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
      } else setProductActionMessage(message);
    } finally {
      setUpdatingProductId("");
    }
  }

  async function submitAdminLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (loggingIn) {
      return;
    }

    const form = new FormData(event.currentTarget);
    const username = String(form.get("username") || "").trim();
    const password = String(form.get("password") || "");

    if (!username || !password) {
      setLoginMessage("Vui lòng nhập tài khoản và mật khẩu admin.");
      return;
    }

    setLoggingIn(true);
    setLoginMessage("");

    try {
      const session = await loginAdmin(username, password);
      setAdminSession(session);
      setLoginMessage("");
      setError("");
    } catch (exception) {
      setAdminSession(null);
      setLoginMessage(exception instanceof Error ? exception.message : "Không đăng nhập được. Vui lòng thử lại.");
    } finally {
      setLoggingIn(false);
    }
  }

  function signOutAdmin() {
    logoutAdmin();
    setAdminSession(null);
    setError("");
    setLoginMessage("Đã đăng xuất khỏi trang quản trị.");
  }

  if (!authReady) {
    return (
      <main className="grid min-h-screen place-items-center bg-[#fff3f6] px-5 text-[#4b2025]">
        <section className="w-full max-w-[420px] rounded-2xl border border-[#ffc6d4] bg-white/82 p-8 text-center shadow-[0_18px_44px_rgba(232,50,108,0.16)]">
          <img src="/images/logo-menu-transparent.png" alt="Hoàn Doãn Beauty & Academy" className="mx-auto h-[78px] w-[220px] object-contain" />
          <p className="mt-5 font-bold text-[#e8326c]">Đang kiểm tra phiên đăng nhập...</p>
        </section>
      </main>
    );
  }

  if (!adminSession) {
    return (
      <main className="grid min-h-screen place-items-center bg-[#fff3f6] bg-[url('/images/admin.png')] bg-cover bg-center px-5 text-[#4b2025]">
        <form onSubmit={submitAdminLogin} className="w-full max-w-[430px] rounded-2xl border border-[#ffc6d4] bg-white/86 p-8 shadow-[0_22px_54px_rgba(232,50,108,0.2)]">
          <img src="/images/logo-menu-transparent.png" alt="Hoàn Doãn Beauty & Academy" className="h-[82px] w-[230px] object-contain object-left" />
          <h1 className="mt-7 font-serif text-[31px] font-bold uppercase leading-tight text-[#b04432]">Đăng nhập admin</h1>
          <p className="mt-2 text-[14px] text-[#7b4b50]">Quản lý lịch hẹn, đơn hàng, sản phẩm và khách hàng.</p>

          <label className="mt-7 block text-[13px] font-bold uppercase text-[#e8326c]">
            Tài khoản
            <input name="username" autoComplete="username" className="mt-2 w-full rounded-xl border border-[#ffc6d4] bg-white px-4 py-3 text-[15px] text-[#4b2025] outline-none transition focus:border-[#e8326c] focus:ring-2 focus:ring-[#ffd6e0]" type="text" />
          </label>

          <label className="mt-4 block text-[13px] font-bold uppercase text-[#e8326c]">
            Mật khẩu
            <input name="password" autoComplete="current-password" className="mt-2 w-full rounded-xl border border-[#ffc6d4] bg-white px-4 py-3 text-[15px] text-[#4b2025] outline-none transition focus:border-[#e8326c] focus:ring-2 focus:ring-[#ffd6e0]" type="password" />
          </label>

          {loginMessage ? <p className="mt-4 rounded-xl border border-[#ffc6d4] bg-[#fff5f8] p-3 text-[13px] font-bold text-[#e8326c]">{loginMessage}</p> : null}

          <button disabled={loggingIn} className="mt-6 w-full rounded-xl bg-[#ef3670] py-3 text-[15px] font-bold text-white shadow-[0_12px_28px_rgba(239,54,112,0.28)] disabled:cursor-not-allowed disabled:opacity-60" type="submit">
            {loggingIn ? "Đang đăng nhập..." : "Đăng nhập"}
          </button>
        </form>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-[#fff3f6] bg-[url('/images/admin.png')] bg-cover bg-fixed bg-center bg-no-repeat text-[#4b2025]">
      <div className="grid min-h-screen grid-cols-[320px_1fr] max-[1100px]:grid-cols-1">
        <aside className="relative overflow-hidden border-r border-[#ffd2dc] bg-white/42 px-[28px] py-[26px] shadow-[14px_0_44px_rgba(232,74,111,0.13)] backdrop-blur-[2px] max-[1100px]:hidden">
          <div className="absolute bottom-0 left-0 h-64 w-full bg-[radial-gradient(circle_at_35%_72%,rgba(255,134,169,0.36),transparent_52%)]" />
          <div className="absolute left-[230px] top-[265px] text-[28px] text-white/80">✦</div>
          <div className="absolute bottom-6 left-8 text-[34px] text-white/75">✿</div>
          <div className="relative">
            <div className="flex items-center justify-between">
              <img
                src="/images/logo-menu-transparent.png"
                alt="Hoàn Doãn Beauty & Academy"
                className="h-[78px] w-[218px] object-contain object-left"
              />
              <button className="grid h-9 w-9 place-items-center rounded-full bg-white text-[24px] text-[#e8326c] shadow-[0_8px_22px_rgba(232,50,108,0.22)]" type="button">›</button>
            </div>

            <nav className="mt-[34px] grid gap-[13px]">
              {menuItems.map(([icon, title, sub]) => {
                const active = activeMenu === title;
                return (
                  <button
                    key={title}
                    onClick={() => setActiveMenu(title)}
                    className={`flex min-h-[64px] items-center gap-4 rounded-xl px-4 py-3 text-left transition ${active ? "bg-white shadow-[0_14px_34px_rgba(232,50,108,0.2)]" : "hover:bg-white/70"}`}
                    type="button"
                  >
                    <span className="grid h-8 w-8 place-items-center text-[21px] text-[#e8326c]">{icon}</span>
                    <span>
                      <strong className="block text-[14px] text-[#e8326c]">{title}</strong>
                      <small className="text-[13px] text-[#5f3339]">{sub}</small>
                    </span>
                    {active ? <em className="ml-auto rounded-full bg-[#ff5b8f] px-3 py-1 text-[12px] not-italic text-white">New</em> : null}
                  </button>
                );
              })}
            </nav>

            <section className="mt-[54px] rounded-2xl border border-[#ffc6d4] bg-white/70 p-6 text-center shadow-[0_16px_34px_rgba(232,50,108,0.18)]">
              <div className="text-[36px]">♕</div>
              <p className="mt-3 text-[11px] uppercase tracking-[0.18em] text-[#d76b76]">Gói dịch vụ hiện tại</p>
              <h2 className="mt-2 font-serif text-[29px] font-bold text-[#e8326c]">VIP PRO MAX</h2>
              <button className="mt-6 w-full rounded-lg bg-[#ef3670] py-3 text-[13px] font-bold text-white shadow-[0_10px_24px_rgba(239,54,112,0.28)]" type="button">Nâng cấp gói dịch vụ</button>
            </section>
          </div>
        </aside>

        <section className="p-[22px_28px_18px] max-[760px]:p-4">
          <header className="mb-[18px] flex items-center justify-between gap-4 max-[900px]:flex-col max-[900px]:items-start">
            <div className="flex items-center gap-4">
              <img
                src="/images/logo-menu-transparent.png"
                alt="Hoàn Doãn Beauty & Academy"
                className="hidden h-[54px] w-[150px] object-contain object-left max-[1100px]:block"
              />
              <span className="text-[34px]">{pageIcon}</span>
              <div>
                <h1 className="font-serif text-[33px] font-bold uppercase leading-[1.05] text-[#b04432] max-[620px]:text-[25px]">{pageTitle}</h1>
                <p className="mt-1 text-[14px] text-[#7b4b50]">{pageSubtitle}</p>
              </div>
            </div>
            <div className="flex items-center gap-4">
              <button className="rounded-xl border border-[#ffc6d4] bg-white/80 px-6 py-3 font-bold text-[#e8326c] shadow-[0_10px_24px_rgba(232,50,108,0.12)]" type="button">♕ VIP PRO MAX</button>
              <div className="flex items-center gap-3 rounded-full bg-white/70 px-3 py-2">
                <img src="/images/products/icon_trang_chu/cut/vip_user_round.png" alt="" className="h-10 w-10 rounded-full object-cover" />
                <span className="text-[13px]"><strong className="block">Hoàn Doãn Admin</strong>Quản trị viên</span>
                <span>⌄</span>
              </div>
              <button onClick={signOutAdmin} className="rounded-xl border border-[#ffc6d4] bg-white/80 px-5 py-3 text-[13px] font-bold text-[#b04432] shadow-[0_10px_24px_rgba(232,50,108,0.1)]" type="button">Đăng xuất</button>
            </div>
          </header>

          {error ? <p className="mb-4 rounded-xl border border-[#ffc6d4] bg-white/80 p-4 text-[#e8326c]">{error}</p> : null}

          {activeMenu === "CONTENT" ? (
            <>
          <section className="mb-[16px] grid grid-cols-4 overflow-hidden rounded-2xl border border-[#ffc6d4] bg-white/65 shadow-[0_12px_34px_rgba(232,50,108,0.12)] max-[900px]:grid-cols-2 max-[560px]:grid-cols-1">
            {steps.map(([num, title, text], index) => {
              const active = index === activeStepIndex;
              const completed = index < activeStepIndex;
              return (
              <article key={num} className={`relative flex min-h-[84px] items-center gap-4 px-7 py-4 transition ${active ? "bg-white shadow-[inset_0_-3px_0_#f33f79]" : completed ? "bg-white/55" : ""}`}>
                <strong className={`grid h-12 w-12 shrink-0 place-items-center rounded-full text-[21px] transition ${active ? "bg-[#f33f79] text-white shadow-[0_8px_18px_rgba(243,63,121,0.35)]" : completed ? "border border-[#f33f79] bg-[#fff4f7] text-[#e8326c]" : "border border-[#ffc6d4] bg-white/45 text-[#b04432]"}`}>{completed ? "✓" : num}</strong>
                <span><b className={`block text-[14px] ${active ? "text-[#f33f79]" : "text-[#e8326c]"}`}>{title}</b><small className="text-[#7b4b50]">{active ? "Đang thực hiện" : completed ? "Đã hoàn tất" : text}</small></span>
                {index < 3 ? <i className="absolute right-0 top-1/2 h-[58px] w-px -translate-y-1/2 bg-[#f8bdcb]" /> : null}
              </article>
              );
            })}
          </section>

          <section className="grid grid-cols-[390px_minmax(360px,0.85fr)_minmax(520px,1.35fr)] gap-[14px] max-[1380px]:grid-cols-1">
            <aside className="rounded-2xl border border-[#ffc6d4] bg-white/72 p-5 shadow-[0_14px_32px_rgba(232,50,108,0.12)]">
              <h2 className="text-[18px] font-bold uppercase text-[#e8326c]">Tải ảnh sản phẩm</h2>
              <div onClick={() => productImageInputRef.current?.click()} className="relative mt-4 cursor-pointer overflow-hidden rounded-xl border border-[#ffd1dc] bg-[#fff5f7]">
                {previewImage ? (
                  <img src={previewImage} alt="Sản phẩm" className="h-[342px] w-full object-cover" />
                ) : (
                  <div className="h-[342px] w-full bg-[#fff5f7]" />
                )}
                {previewImage ? (
                  <button onClick={(event) => { event.stopPropagation(); clearAiResult(); }} className="absolute right-3 top-3 grid h-8 w-8 place-items-center rounded-lg bg-white/92 text-[22px] text-[#4b2025] shadow-[0_6px_16px_rgba(73,32,37,0.12)]" type="button">×</button>
                ) : null}
              </div>
              <input ref={productImageInputRef} onChange={uploadImage} className="hidden" type="file" accept="image/jpeg,image/png,image/webp" />
              <button onClick={identifyProduct} disabled={identifying || findingOfficialUrl || !productImageFile} className="mt-3 w-full rounded-lg bg-[#f33f79] py-3 font-bold text-white shadow-[0_10px_24px_rgba(243,63,121,0.28)] disabled:cursor-not-allowed disabled:opacity-60" type="button">
                {identifying ? "Đang tìm bằng ảnh..." : findingOfficialUrl ? "Đang tìm URL..." : "Tìm URL & nhận diện"}
              </button>

              <section className="mt-4 rounded-xl border border-[#ffc6d4] bg-white/70 p-4">
                <h3 className="font-bold uppercase text-[#e8326c]">Trạng thái thao tác</h3>
                <p className="mt-3 whitespace-pre-wrap rounded-lg bg-[#fff4f7] p-3 text-[13px] font-bold leading-6 text-[#e8326c]">{workflowStatus}</p>
              </section>
            </aside>

            <section className="rounded-2xl border border-[#ffc6d4] bg-white/72 p-5 shadow-[0_14px_32px_rgba(232,50,108,0.12)]">
              <h2 className="text-[18px] font-bold uppercase text-[#e8326c]">Kết quả nhận diện</h2>
              {identification ? (
                <div className="mt-4 grid gap-3">
                  <div className="rounded-xl border border-[#ffd1dc] bg-[#fff8f9] p-4">
                    <div className="flex items-center justify-between gap-3">
                      <span className="text-[13px] font-bold text-[#7b4b50]">Độ tin cậy</span>
                      <strong className={`text-[22px] ${identification.confidence >= 75 ? "text-[#18875f]" : "text-[#e8326c]"}`}>{identification.confidence}%</strong>
                    </div>
                    {identification.needsConfirmation ? (
                      <p className="mt-3 text-[13px] font-bold text-[#e8326c]">Hoàn Doãn Beauty & Academy cần bạn xác nhận lại sản phẩm hoặc tải ảnh rõ hơn.</p>
                    ) : null}
                  </div>

                  {[
                    ["Tên sản phẩm", confirmedName, setConfirmedName],
                    ["Thương hiệu", confirmedBrand, setConfirmedBrand],
                    ["Loại sản phẩm", confirmedCategory, setConfirmedCategory],
                    ["Dòng/phiên bản", confirmedVariant, setConfirmedVariant],
                    ["Mã màu", confirmedShade, setConfirmedShade],
                    ["Finish/kết cấu", confirmedFinish, setConfirmedFinish],
                    ["Dung tích/trọng lượng", confirmedSize, setConfirmedSize]
                  ].filter(([, value]) => hasDisplayValue(cleanRecognizedField(value as string))).map(([label, value, setter]) => (
                    <label key={label as string} className="grid gap-1 text-[13px] font-bold text-[#7b4b50]">
                      {label as string}
                      <input value={value as string} onChange={(event) => (setter as (next: string) => void)(event.target.value)} className="rounded-lg border border-[#ffc6d4] bg-white px-3 py-2 text-[14px] font-normal text-[#3f2427] outline-none focus:border-[#f33f79]" />
                    </label>
                  ))}

                  {identification.visibleText.some((item) => hasDisplayValue(cleanRecognizedField(item))) ? (
                    <div className="rounded-xl border border-[#ffd1dc] bg-white/75 p-4">
                      <h3 className="text-[13px] font-bold uppercase text-[#e8326c]">Chữ nhìn thấy trên bao bì</h3>
                      <p className="mt-2 text-[13px] leading-6 text-[#5f3339]">{identification.visibleText.map(cleanRecognizedField).filter(hasDisplayValue).join(", ")}</p>
                    </div>
                  ) : null}

                  <label className="grid gap-1 text-[13px] font-bold text-[#7b4b50]">
                    URL trang sản phẩm đáng tin cậy
                    <div className="grid grid-cols-[1fr_auto_auto] gap-2 max-[560px]:grid-cols-1">
                      <input value={optionalInfo.officialProductUrl} onChange={(event) => {
                        setVerifiedOfficialUrl("");
                        setOptionalInfo({ ...optionalInfo, officialProductUrl: event.target.value });
                      }} className="min-w-0 rounded-lg border border-[#ffc6d4] bg-white px-3 py-2 text-[14px] font-normal text-[#3f2427] outline-none focus:border-[#f33f79]" placeholder="https://..." />
                      <a
                        href={hasOfficialProductUrl ? currentOfficialUrl : undefined}
                        target="_blank"
                        rel="noreferrer"
                        aria-disabled={!hasOfficialProductUrl}
                        className={`rounded-lg border border-[#ffc6d4] bg-white px-3 py-2 text-center text-[12px] font-bold text-[#e8326c] ${hasOfficialProductUrl ? "" : "pointer-events-none opacity-50"}`}
                      >
                        Mở link
                      </a>
                      <button
                        onClick={() => copyText(currentOfficialUrl)}
                        disabled={!hasOfficialProductUrl}
                        className="rounded-lg border border-[#ffc6d4] bg-white px-3 py-2 text-[12px] font-bold text-[#e8326c] disabled:cursor-not-allowed disabled:opacity-50"
                        type="button"
                      >
                        Sao chép
                      </button>
                    </div>
                    <span className="text-[12px] font-normal text-[#8b5a60]">
                      {currentOfficialUrl && !officialUrlMatchesCurrentProduct
                        ? "URL này chưa được backend xác minh nên chưa được mở hoặc dùng để viết bài."
                        : "Chỉ URL đã được backend xác minh đúng sản phẩm mới được dùng để viết bài sale."}
                    </span>
                  </label>

                  <button onClick={() => writeArticle(false)} disabled={!canWrite || writing} className="rounded-lg bg-[#f33f79] py-3 font-bold text-white shadow-[0_10px_24px_rgba(243,63,121,0.28)] disabled:cursor-not-allowed disabled:opacity-60" type="button">
                    {writing ? "Đang đọc link và viết bài..." : hasOfficialProductUrl ? "Viết bài sale" : "Cần URL xác minh"}
                  </button>
                </div>
              ) : pendingIdentification ? (
                <div className="mt-4 grid gap-3">
                  <div className="rounded-xl border border-[#ffd1dc] bg-[#fff8f9] p-4">
                    <div className="flex items-center justify-between gap-3">
                      <span className="text-[13px] font-bold text-[#7b4b50]">Đã đọc từ ảnh</span>
                      <strong className={`text-[22px] ${pendingIdentification.confidence >= 75 ? "text-[#18875f]" : "text-[#e8326c]"}`}>{pendingIdentification.confidence}%</strong>
                    </div>
                    <p className="mt-3 text-[13px] leading-6 text-[#5f3339]">
                      {[
                        cleanRecognizedField(pendingIdentification.productName),
                        cleanRecognizedField(pendingIdentification.brand),
                        cleanRecognizedField(pendingIdentification.variant),
                        cleanRecognizedField(pendingIdentification.shade),
                        cleanRecognizedField(pendingIdentification.size)
                      ].filter(hasDisplayValue).join(" • ") || "Cần thêm tên sản phẩm hoặc URL để ghi kết quả chắc hơn."}
                    </p>
                  </div>

                  <label className="grid gap-1 text-[13px] font-bold text-[#7b4b50]">
                    Dán URL trang sản phẩm đáng tin cậy
                    <div className="grid grid-cols-[1fr_auto] gap-2 max-[560px]:grid-cols-1">
                      <input
                        value={optionalInfo.officialProductUrl}
                        onChange={(event) => {
                          setVerifiedOfficialUrl("");
                          setOptionalInfo({ ...optionalInfo, officialProductUrl: event.target.value });
                        }}
                        className="min-w-0 rounded-lg border border-[#ffc6d4] bg-white px-3 py-2 text-[14px] font-normal text-[#3f2427] outline-none focus:border-[#f33f79]"
                        placeholder="https://..."
                      />
                      <button
                        onClick={confirmPendingOfficialUrl}
                        disabled={!optionalInfo.officialProductUrl.trim() || findingOfficialUrl}
                        className="rounded-lg bg-[#f33f79] px-4 py-2 text-[13px] font-bold text-white shadow-[0_10px_24px_rgba(243,63,121,0.22)] disabled:cursor-not-allowed disabled:opacity-60"
                        type="button"
                      >
                        {findingOfficialUrl ? "Đang xác minh..." : "Ghi kết quả"}
                      </button>
                    </div>
                    <span className="text-[12px] font-normal text-[#8b5a60]">Khi bấm ghi kết quả, URL này sẽ được lưu cùng kết quả nhận diện và dùng tiếp để viết bài sale.</span>
                  </label>
                </div>
              ) : (
                <p className="mt-4 rounded-xl border border-[#ffd1dc] bg-[#fff8f9] p-4 text-[14px] leading-6 text-[#5f3339]">Sau khi tải ảnh, bấm “Tìm URL & nhận diện”. Hệ thống chỉ ghi kết quả khi đã tìm được URL trang sản phẩm đáng tin cậy.</p>
              )}
            </section>

            <section className="rounded-2xl border border-[#ffc6d4] bg-white/72 p-5 shadow-[0_14px_32px_rgba(232,50,108,0.12)]">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 className="text-[18px] font-bold uppercase text-[#e8326c]">Bài sale hoàn chỉnh</h2>
                <span className="text-[13px] font-bold text-[#f33f79]">{Math.min(wordCount, 1000)}/1000 từ</span>
              </div>

              <div className="mt-4 grid grid-cols-3 gap-3 max-[760px]:grid-cols-1">
                {[
                  ["Giá bán", "price"],
                  ["Giá khuyến mãi", "salePrice"],
                  ["Quà tặng", "gift"],
                  ["Tên shop", "shopName"],
                  ["Số điện thoại", "phone"],
                  ["Địa chỉ", "address"],
                  ["Website", "website"],
                  ["Số lượng còn lại", "remainingQuantity"]
                ].map(([label, key]) => (
                  <label key={key} className="grid gap-1 text-[12px] font-bold text-[#7b4b50]">
                    {label}{lockedOptionalFields.has(key) ? " cố định" : ""}
                    <input
                      value={optionalInfo[key as keyof typeof optionalInfo]}
                      onChange={(event) => setOptionalInfo({ ...optionalInfo, [key]: event.target.value })}
                      readOnly={lockedOptionalFields.has(key)}
                      className={`rounded-lg border border-[#ffc6d4] px-3 py-2 text-[13px] font-normal text-[#3f2427] outline-none focus:border-[#f33f79] ${lockedOptionalFields.has(key) ? "bg-[#fff4f7] font-bold text-[#7b4b50]" : "bg-white"}`}
                    />
                  </label>
                ))}
              </div>

              {saleResult?.researchSuccessful ? (
                <div className="mt-4 grid gap-4">
                  <article className="grid gap-4 rounded-xl border border-[#ffc6d4] bg-white/78 p-5">
                    {saleResult.headline ? <h3 className="font-serif text-[25px] font-bold leading-tight text-[#b04432]">{cleanSaleText(saleResult.headline)}</h3> : null}
                    {saleResult.opening ? <p className="whitespace-pre-wrap text-[15px] leading-7 text-[#3f2427]">{cleanSaleText(saleResult.opening)}</p> : null}
                    {saleResult.contentBlocks.map((block, index) => (
                      <SaleContentBlock key={`${block.type}-${block.title}-${index}`} block={block} />
                    ))}
                    <SalePriceBlock price={optionalInfo.price} salePrice={optionalInfo.salePrice} remainingQuantity={optionalInfo.remainingQuantity} gift={optionalInfo.gift} />
                    {formatCallToAction(saleResult.callToAction) ? (
                      <section className="rounded-xl bg-[#e8326c] p-4 text-white shadow-[0_10px_24px_rgba(232,50,108,0.22)]">
                        <h3 className="text-[14px] font-bold uppercase">CTA</h3>
                        <p className="mt-2 whitespace-pre-wrap text-[15px] leading-7">{formatCallToAction(saleResult.callToAction)}</p>
                      </section>
                    ) : null}
                    <ContactBlock contact={saleResult.contact} />
                    {saleResult.hashtags.length ? <p className="text-[14px] font-bold leading-7 text-[#e8326c]">{saleResult.hashtags.join(" ")}</p> : null}
                  </article>

                  {hasDisplayValue(saleResult.shortCaption) ? <ResultBlock title="Caption ngắn" value={saleResult.shortCaption} /> : null}

                  <details className="rounded-xl border border-[#ffd1dc] bg-[#fff8f9] p-4">
                    <summary className="cursor-pointer text-[14px] font-bold uppercase text-[#e8326c]">Thông tin chi tiết</summary>
                    <div className="mt-4 grid gap-4">
                      <ResultBlock title="Claim đã xác minh" value={saleResult.verifiedDetails.claims.map((claim) => claim.claim).join("\n")} />
                      <ResultBlock title="Cách dùng" value={saleResult.verifiedDetails.usage.join("\n")} />
                      <ResultBlock title="Lưu ý" value={saleResult.verifiedDetails.warnings.join("\n")} />
                      <section className="rounded-xl border border-[#ffd1dc] bg-white/80 p-4">
                        <h3 className="text-[14px] font-bold uppercase text-[#e8326c]">Thông tin tham khảo</h3>
                        <div className="mt-3 grid gap-2">
                          {saleResult.verifiedDetails.sources.map((source) => (
                            <a key={source.url} href={source.url} target="_blank" rel="noreferrer" className="rounded-lg bg-white px-3 py-2 text-[13px] text-[#5f3339] hover:text-[#e8326c]">
                              <strong className="block">{source.website}</strong>
                              <span>{source.title}</span>
                            </a>
                          ))}
                        </div>
                      </section>
                    </div>
                  </details>

                  <div className="grid grid-cols-5 gap-3 max-[900px]:grid-cols-2 max-[560px]:grid-cols-1">
                    <button onClick={() => copyText(mainArticleText, mainArticleHtml, mainArticleRtf)} className="rounded-lg border border-[#ffc6d4] bg-white py-3 font-bold text-[#e8326c]" type="button">Sao chép bài viết</button>
                    {hasDisplayValue(saleResult.shortCaption) ? <button onClick={() => copyText(saleResult.shortCaption)} className="rounded-lg border border-[#ffc6d4] bg-white py-3 font-bold text-[#e8326c]" type="button">Sao chép caption</button> : null}
                    <button onClick={() => writeArticle(true)} disabled={writing || !canWrite} className="rounded-lg border border-[#ffc6d4] bg-white py-3 font-bold text-[#e8326c] disabled:opacity-60" type="button">Viết lại</button>
                    <button onClick={() => setSaleResult(null)} className="rounded-lg bg-[#4b2025] py-3 font-bold text-white" type="button">Xóa kết quả</button>
                  </div>
                </div>
              ) : (
                <div className="mt-4 rounded-xl border border-[#ffd1dc] bg-[#fff8f9] p-5 text-[14px] leading-6 text-[#5f3339]">
                  {saleResult?.warningMessage || "Bài sale sẽ xuất hiện sau khi sản phẩm được xác nhận và có thông tin tham khảo đáng tin cậy."}
                </div>
              )}
            </section>
          </section>

          <section className="mt-[14px] grid grid-cols-5 gap-4 rounded-2xl border border-[#ffc6d4] bg-white/72 p-4 shadow-[0_14px_32px_rgba(232,50,108,0.12)] max-[1200px]:grid-cols-2 max-[620px]:grid-cols-1">
            {[
              ["Sản phẩm thật", String(products.length), "Dữ liệu từ backend", "▤"],
              ["Khách hàng thật", String(customers.length), "Dữ liệu từ backend", "◎"],
              ["Đơn hàng hôm nay", dashboard ? String(dashboard.newOrders) : "...", "Dữ liệu từ backend", "▥"],
              ["Lịch hẹn hôm nay", dashboard ? String(dashboard.appointmentsToday) : "...", "Dữ liệu từ backend", "▣"],
              ["Tổng đơn thật", String(orders.length), "Dữ liệu từ backend", "🏆"]
            ].map(([label, value, sub, icon]) => (
              <article key={label} className="rounded-xl border border-[#ffc6d4] bg-[#fff8f9] p-4">
                <div className="flex items-center justify-between gap-3">
                  <span><small className="block text-[12px] text-[#5f3339]">{label}</small><strong className="mt-1 block text-[21px] text-[#32191d]">{value}</strong></span>
                  <em className="text-[34px] not-italic text-[#f33f79]">{icon}</em>
                </div>
                <p className="mt-1 text-[12px] text-[#7b4b50]">{sub}</p>
              </article>
            ))}
          </section>
            </>
          ) : (
            <AdminView
              activeMenu={activeMenu}
              dashboard={dashboard}
              appointments={appointments}
              orders={orders}
              products={products}
              customers={customers}
              appointmentActionMessage={appointmentActionMessage}
              updatingAppointmentId={updatingAppointmentId}
              appointmentsLoading={loadingAdminData}
              onAppointmentStatusChange={changeAppointmentStatus}
              onAppointmentFiltersChange={changeAppointmentFilters}
              orderActionMessage={orderActionMessage}
              updatingOrderId={updatingOrderId}
              onOrderStatusChange={changeOrderStatus}
              productActionMessage={productActionMessage}
              updatingProductId={updatingProductId}
              onProductSave={saveProduct}
              onProductDelete={removeProduct}
            />
          )}
        </section>
      </div>
    </main>
  );
}
