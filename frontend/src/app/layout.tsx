import type { Metadata, Viewport } from "next";
import { Allura, Cormorant_Garamond, Montserrat } from "next/font/google";
import { ScrollReveal } from "@/components/ScrollReveal";
import { ZoomLock } from "@/components/ZoomLock";
import "./globals.css";

const cormorant = Cormorant_Garamond({
  subsets: ["latin", "vietnamese"],
  variable: "--font-cormorant",
  weight: ["400", "500", "600"]
});

const allura = Allura({
  subsets: ["latin"],
  variable: "--font-allura",
  weight: "400"
});

const montserrat = Montserrat({
  subsets: ["latin", "vietnamese"],
  variable: "--font-montserrat",
  weight: ["400", "500", "600", "700"]
});

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
      <body className={`${cormorant.variable} ${allura.variable} ${montserrat.variable}`}>
        <ZoomLock />
        <ScrollReveal />
        {children}
      </body>
    </html>
  );
}
