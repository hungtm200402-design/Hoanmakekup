"use client";

import { useEffect } from "react";

export function ZoomLock() {
  useEffect(() => {
    const preventZoomKeys = (event: KeyboardEvent) => {
      if (!event.ctrlKey && !event.metaKey) return;

      const key = event.key.toLowerCase();
      if (key === "+" || key === "-" || key === "=" || key === "0" || key === "_") {
        event.preventDefault();
      }
    };

    const preventZoomWheel = (event: WheelEvent) => {
      if (event.ctrlKey || event.metaKey) {
        event.preventDefault();
      }
    };

    const preventGesture = (event: Event) => {
      event.preventDefault();
    };

    window.addEventListener("keydown", preventZoomKeys, { passive: false });
    window.addEventListener("wheel", preventZoomWheel, { passive: false });
    window.addEventListener("gesturestart", preventGesture, { passive: false });
    window.addEventListener("gesturechange", preventGesture, { passive: false });
    window.addEventListener("gestureend", preventGesture, { passive: false });

    return () => {
      window.removeEventListener("keydown", preventZoomKeys);
      window.removeEventListener("wheel", preventZoomWheel);
      window.removeEventListener("gesturestart", preventGesture);
      window.removeEventListener("gesturechange", preventGesture);
      window.removeEventListener("gestureend", preventGesture);
    };
  }, []);

  return null;
}
