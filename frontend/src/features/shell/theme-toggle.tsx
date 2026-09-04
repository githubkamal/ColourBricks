"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { ACCENTS } from "@/lib/theme";
import { cn } from "@/lib/utils";
import { useTheme } from "./theme-context";

function SunIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-4">
      <circle cx="12" cy="12" r="4" />
      <path
        strokeLinecap="round"
        d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"
      />
    </svg>
  );
}

function MoonIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-4">
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z"
      />
    </svg>
  );
}

/** Light/dark toggle plus an accent-colour swatch picker (client request, 2026-09-04). */
export function ThemeToggle() {
  const { mode, accent, setMode, setAccent } = useTheme();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onClickAway(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onClickAway);
    return () => document.removeEventListener("mousedown", onClickAway);
  }, []);

  return (
    <div className="flex items-center gap-1">
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        aria-label={mode === "dark" ? "Switch to light mode" : "Switch to dark mode"}
        onClick={() => setMode(mode === "dark" ? "light" : "dark")}
      >
        {mode === "dark" ? <SunIcon /> : <MoonIcon />}
      </Button>

      <div ref={containerRef} className="relative">
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          aria-label="Choose accent colour"
          aria-expanded={open}
          onClick={() => setOpen((v) => !v)}
        >
          <span
            className="size-3.5 rounded-full ring-1 ring-black/10"
            style={{ backgroundColor: ACCENTS.find((a) => a.id === accent)?.swatch }}
          />
        </Button>

        {open && (
          <div className="bg-popover border-border absolute right-0 z-40 mt-1 flex gap-1.5 rounded-lg border p-2 shadow-md">
            {ACCENTS.map((a) => (
              <button
                key={a.id}
                type="button"
                aria-label={a.label}
                aria-pressed={accent === a.id}
                title={a.label}
                className={cn(
                  "size-6 rounded-full ring-1 ring-black/10 transition-transform hover:scale-110",
                  accent === a.id && "ring-ring ring-2 ring-offset-2 ring-offset-[var(--popover)]",
                )}
                style={{ backgroundColor: a.swatch }}
                onClick={() => {
                  setAccent(a.id);
                  setOpen(false);
                }}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
