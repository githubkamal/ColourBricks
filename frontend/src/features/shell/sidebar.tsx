"use client";

import { ChevronDown } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { visibleNavigation, type NavSection } from "@/lib/navigation";
import { cn } from "@/lib/utils";

/**
 * The single best-matching href for the current path across the whole nav
 * tree — the longest target that's an exact or prefix match — so a parent
 * route (e.g. "/vendors") never lights up alongside a more specific sibling
 * ("/vendors/purchase-orders") that's actually the current page.
 */
function bestMatchHref(sections: NavSection[], pathname: string): string | null {
  let best: string | null = null;
  for (const section of sections) {
    for (const item of section.items) {
      const target = item.href.split("?")[0];
      const matches = pathname === target || pathname.startsWith(`${target}/`);
      if (matches && (best === null || target.length > best.length)) {
        best = item.href;
      }
    }
  }
  return best;
}

export function Sidebar({ permissions, rail = false }: { permissions: string[]; rail?: boolean }) {
  const pathname = usePathname();
  const sections = visibleNavigation(permissions);
  const activeHref = bestMatchHref(sections, pathname);

  return (
    <div className="flex h-full flex-col">
      <div
        className={cn(
          "border-sidebar-border flex h-14 shrink-0 items-center gap-2.5 border-b px-4",
          rail && "justify-center px-2",
        )}
      >
        <span
          className="text-sidebar-primary-foreground font-heading flex size-8 shrink-0 items-center justify-center rounded-lg text-sm font-semibold"
          style={{
            backgroundColor: "var(--sidebar-primary)",
            backgroundImage: "var(--primary-gradient, none)",
          }}
        >
          CB
        </span>
        {!rail && (
          <span className="min-w-0 leading-tight">
            <span className="font-heading block truncate text-[15px] font-semibold">
              Colour Bricks
            </span>
            <span className="text-sidebar-foreground/55 block truncate text-[11px]">
              Fund &amp; site ledger
            </span>
          </span>
        )}
      </div>

      <nav
        aria-label="Primary"
        className={cn("flex-1 scrollbar-thin space-y-1 overflow-y-auto px-3 py-3", rail && "px-2")}
      >
        {sections.map((section) =>
          rail ? (
            <RailSection key={section.label} section={section} activeHref={activeHref} />
          ) : (
            <SidebarSection key={section.label} section={section} activeHref={activeHref} />
          ),
        )}
      </nav>
    </div>
  );
}

function SidebarSection({
  section,
  activeHref,
}: {
  section: NavSection;
  activeHref: string | null;
}) {
  const active = section.items.some((item) => item.href === activeHref);
  const [open, setOpen] = useState(active);
  const [wasActive, setWasActive] = useState(active);
  const Icon = section.icon;

  // A navigation elsewhere in the app can make this section active without
  // this component ever unmounting — auto-expand when that happens, but
  // never fight a user's manual collapse of an inactive section. Derived
  // during render (React's documented escape hatch for "adjust state when a
  // prop changes") rather than an effect, which would set state one render
  // late and cascade an extra render besides.
  if (active !== wasActive) {
    setWasActive(active);
    if (active) setOpen(true);
  }

  return (
    <div>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className={cn(
          "text-sidebar-foreground/65 hover:text-sidebar-foreground flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-[11px] font-semibold tracking-wide uppercase transition-colors",
          active && "text-sidebar-foreground",
        )}
      >
        <Icon className="size-3.5 shrink-0" aria-hidden="true" />
        <span className="flex-1 truncate">{section.label}</span>
        <ChevronDown
          className={cn("size-3.5 shrink-0 transition-transform", !open && "-rotate-90")}
          aria-hidden="true"
        />
      </button>

      {/* A plain max-height collapse, not the grid-template-rows 0fr/1fr trick —
          that relies on a grid row's minimum size respecting the item's
          `min-height: 0`, and in practice left a sliver of the first item's
          background visible after collapsing (reported by the client,
          2026-09-07). `max-height: 0` has no such min-content interaction:
          it's an unconditional clip, so nothing can bleed through. */}
      <div
        className={cn(
          "overflow-hidden transition-[max-height] duration-200 ease-out",
          open ? "max-h-[600px]" : "max-h-0",
        )}
      >
        <ul aria-hidden={!open} className="mt-0.5 space-y-0.5 pb-2">
          {section.items.map((item) => {
            const itemActive = item.href === activeHref;
            return (
              <li key={`${item.href}:${item.label}`}>
                <Link
                  href={item.href}
                  tabIndex={open ? undefined : -1}
                  aria-current={itemActive ? "page" : undefined}
                  className={cn(
                    "relative block rounded-md py-1.5 pr-2.5 pl-6 text-sm transition-colors",
                    itemActive
                      ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                      : "text-sidebar-foreground/75 hover:bg-sidebar-accent/50 hover:text-sidebar-foreground",
                  )}
                >
                  {itemActive && (
                    <span
                      className="bg-sidebar-primary absolute top-1/2 left-0 h-4 w-0.5 -translate-y-1/2 rounded-full"
                      aria-hidden="true"
                    />
                  )}
                  {item.label}
                </Link>
              </li>
            );
          })}
        </ul>
      </div>
    </div>
  );
}

/**
 * Icon-only rail: hover reveals the section's items as a flyout panel.
 * Rendered through a portal at document.body, positioned from the trigger
 * button's real screen coordinates — not CSS `absolute` inside the nav.
 * The nav scrolls vertically (`overflow-y-auto`), and CSS has no way to keep
 * one axis clipped while leaving the other visible on the same element (the
 * spec forces `overflow-x` to `auto` too the moment `overflow-y` isn't
 * `visible`) — so a plain absolutely-positioned flyout gets its horizontal
 * overflow clipped by the nav's own boundary no matter what z-index says.
 * A portal sidesteps that entirely.
 */
function RailSection({ section, activeHref }: { section: NavSection; activeHref: string | null }) {
  const active = section.items.some((item) => item.href === activeHref);
  const Icon = section.icon;
  const buttonRef = useRef<HTMLButtonElement>(null);
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [pos, setPos] = useState<{ top: number; left: number } | null>(null);

  useEffect(
    () => () => {
      if (closeTimer.current) clearTimeout(closeTimer.current);
    },
    [],
  );

  function open() {
    if (closeTimer.current) {
      clearTimeout(closeTimer.current);
      closeTimer.current = null;
    }
    const rect = buttonRef.current?.getBoundingClientRect();
    if (rect) setPos({ top: rect.top, left: rect.right + 4 });
  }

  // A short grace period, not an immediate close — the mouse crosses a few
  // pixels of gap moving from the icon to the flyout beside it, and both
  // elements are separate portalled trees now (no shared hover ancestor to
  // cover that gap the way nested DOM + :hover used to).
  function scheduleClose() {
    closeTimer.current = setTimeout(() => setPos(null), 150);
  }

  return (
    <div onMouseEnter={open} onMouseLeave={scheduleClose}>
      <button
        ref={buttonRef}
        type="button"
        aria-label={section.label}
        className={cn(
          "text-sidebar-foreground/65 hover:bg-sidebar-accent hover:text-sidebar-foreground flex w-full items-center justify-center rounded-md py-2 transition-colors",
          active && "bg-sidebar-accent text-sidebar-accent-foreground",
        )}
      >
        <Icon className="size-4.5 shrink-0" aria-hidden="true" />
      </button>

      {pos &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            style={{ top: pos.top, left: pos.left }}
            className="animate-in fade-in-0 fixed z-50 min-w-44 duration-100"
            onMouseEnter={open}
            onMouseLeave={scheduleClose}
          >
            <div className="bg-popover border-border text-popover-foreground rounded-md border p-1.5 shadow-lg">
              <p className="text-muted-foreground px-2 pb-1 text-[11px] font-semibold tracking-wide uppercase">
                {section.label}
              </p>
              <ul className="space-y-0.5">
                {section.items.map((item) => {
                  const itemActive = item.href === activeHref;
                  return (
                    <li key={`${item.href}:${item.label}`}>
                      <Link
                        href={item.href}
                        aria-current={itemActive ? "page" : undefined}
                        className={cn(
                          "block rounded-md px-2 py-1.5 text-sm whitespace-nowrap transition-colors",
                          itemActive
                            ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                            : "hover:bg-muted",
                        )}
                      >
                        {item.label}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </div>
          </div>,
          document.body,
        )}
    </div>
  );
}
