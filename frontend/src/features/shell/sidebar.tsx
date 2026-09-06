"use client";

import { ChevronDown } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { visibleNavigation, type NavSection } from "@/lib/navigation";
import { cn } from "@/lib/utils";

function sectionIsActive(section: NavSection, pathname: string): boolean {
  return section.items.some((item) => {
    const target = item.href.split("?")[0];
    return pathname === target || pathname.startsWith(`${target}/`);
  });
}

export function Sidebar({ permissions }: { permissions: string[] }) {
  const pathname = usePathname();
  const sections = visibleNavigation(permissions);

  return (
    <div className="flex h-full flex-col">
      <div className="border-sidebar-border flex h-14 shrink-0 items-center gap-2.5 border-b px-4">
        <span className="bg-sidebar-primary text-sidebar-primary-foreground font-heading flex size-8 shrink-0 items-center justify-center rounded-lg text-sm font-semibold">
          CB
        </span>
        <span className="min-w-0 leading-tight">
          <span className="font-heading block truncate text-[15px] font-semibold">
            Colour Bricks
          </span>
          <span className="text-sidebar-foreground/55 block truncate text-[11px]">
            Fund &amp; site ledger
          </span>
        </span>
      </div>

      <nav aria-label="Primary" className="flex-1 space-y-1 overflow-y-auto px-3 py-3">
        {sections.map((section) => (
          <SidebarSection key={section.label} section={section} pathname={pathname} />
        ))}
      </nav>
    </div>
  );
}

function SidebarSection({ section, pathname }: { section: NavSection; pathname: string }) {
  const [open, setOpen] = useState(true);
  const Icon = section.icon;
  const active = sectionIsActive(section, pathname);

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

      {open && (
        <ul className="mt-0.5 space-y-0.5 pb-2">
          {section.items.map((item) => {
            const target = item.href.split("?")[0];
            const itemActive = pathname === target || pathname.startsWith(`${target}/`);
            return (
              <li key={`${item.href}:${item.label}`}>
                <Link
                  href={item.href}
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
      )}
    </div>
  );
}
