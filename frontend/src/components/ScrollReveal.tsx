"use client";

import { useEffect } from "react";

const revealTargets = [
  ".home-frame-content",
  ".home-footer-content > div",
  ".service-category-panel",
  ".service-advice",
  ".service-card",
  ".service-stage .home-footer-content > div",
  ".product-shop-card",
  ".product-benefits > div",
  ".rounded-xl.border"
].join(",");

export function ScrollReveal() {
  useEffect(() => {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      return;
    }

    const elements = Array.from(document.querySelectorAll<HTMLElement>(revealTargets));
    const seen = new WeakSet<HTMLElement>();

    elements.forEach((element) => {
      if (seen.has(element) || element.closest("header")) return;
      seen.add(element);
      element.classList.add("scroll-reveal");
    });

    const revealGroups = Array.from(document.querySelectorAll<HTMLElement>(".home-main-sections, .service-card-grid, .home-footer-content"));
    revealGroups.forEach((group) => {
      Array.from(group.querySelectorAll<HTMLElement>(".scroll-reveal")).forEach((item, index) => {
        item.style.setProperty("--reveal-delay", `${Math.min(index * 70, 420)}ms`);
      });
    });

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        });
      },
      {
        threshold: 0.12,
        rootMargin: "0px 0px -8% 0px"
      }
    );

    elements.forEach((element) => observer.observe(element));

    return () => observer.disconnect();
  }, []);

  return null;
}
