import type { Metadata, Viewport } from "next";
import { ZoomLock } from "@/components/ZoomLock";
import "./globals.css";

export const metadata: Metadata = {
  title: "Hoàn Makeup Beauty & Academy",
  description: "Website makeup và mỹ phẩm theo bộ giao diện Figma đã cung cấp."
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  userScalable: false
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="vi">
      <body>
        <ZoomLock />
        {children}
      </body>
    </html>
  );
}
