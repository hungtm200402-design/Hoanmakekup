import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Hoàn Makeup Beauty & Academy",
  description: "Website makeup và mỹ phẩm theo bộ giao diện Figma đã cung cấp."
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="vi">
      <body>{children}</body>
    </html>
  );
}
