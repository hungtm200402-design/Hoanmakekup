"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { MenuIcon } from "./Icons";
import { navItems } from "@/lib/data";

export function Header() {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  return (
    <header className="relative z-50 bg-white pb-5 pt-5 max-[760px]:pb-3 max-[760px]:pt-3">
      <div className="container-beauty rounded-[22px] border border-[#f4ccd5] bg-white/95 px-8 shadow-[0_12px_32px_rgba(217,46,85,0.13)] max-[760px]:rounded-2xl max-[760px]:px-4">
        <div className="flex min-h-[124px] items-center justify-between gap-7 max-[760px]:min-h-[74px]">
          <Link href="/" className="flex w-[286px] shrink-0 justify-center border-r border-brand-line pr-8 max-[1020px]:w-[230px] max-[760px]:w-[170px] max-[760px]:border-r-0 max-[760px]:pr-0" aria-label="Hoàn Doãn Beauty & Academy">
            <img
              src="/images/logo-menu-transparent.png"
              alt="Hoàn Doãn Beauty & Academy"
              className="h-[98px] w-full object-contain max-[760px]:h-[58px]"
            />
          </Link>

          <nav className="hidden flex-1 items-stretch justify-between gap-2 min-[1020px]:flex">
            {navItems.map((item) => {
              const active = pathname === item.href || (item.href !== "/" && pathname.startsWith(item.href));
              return (
                <Link key={item.label} href={item.href} className={`group relative grid min-w-[86px] place-items-center gap-2 px-2 py-4 text-center text-[13px] font-bold uppercase transition ${active ? "text-brand-red" : "text-brand-ink hover:text-brand-red"}`}>
                  <span className="relative">
                    <img src={item.icon} alt="" className="mx-auto h-[42px] w-[42px] object-contain" />
                    {item.badge ? <span className="absolute -right-3 -top-2 grid h-6 w-6 place-items-center rounded-full bg-brand-red text-[12px] text-white">{item.badge}</span> : null}
                  </span>
                  <span className="whitespace-nowrap">{item.label}</span>
                  {active ? <span className="absolute bottom-0 h-1 w-[78px] rounded-full bg-brand-red" /> : null}
                </Link>
              );
            })}
          </nav>

          <button className="grid h-11 w-11 place-items-center rounded-xl border border-brand-line text-brand-red min-[1020px]:hidden" type="button" aria-label="Mở menu" onClick={() => setOpen((value) => !value)}>
            <MenuIcon />
          </button>
        </div>

        {open ? (
          <nav className="grid gap-1 border-t border-brand-line py-3 text-[13px] font-bold uppercase min-[1020px]:hidden">
            {navItems.map((item) => (
              <Link key={item.label} href={item.href} className="flex items-center gap-3 rounded-lg px-2 py-3 hover:bg-brand-pale" onClick={() => setOpen(false)}>
                <img src={item.icon} alt="" className="h-7 w-7 object-contain" />
                {item.label}
              </Link>
            ))}
          </nav>
        ) : null}
      </div>
    </header>
  );
}
