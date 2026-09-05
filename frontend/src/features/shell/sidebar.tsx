"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { visibleNavigation } from "@/lib/navigation";
import { cn } from "@/lib/utils";

export function Sidebar({ permissions }: { permissions: string[] }) {
  const pathname = usePathname();
  const sections = visibleNavigation(permissions);

  return (
    <nav aria-label="Primary" className="flex h-full flex-col gap-5 overflow-y-auto px-3 py-4">
      {sections.map((section) => (
        <div key={section.label}>
          <p className="text-muted-foreground px-2 pb-1 text-[11px] font-semibold tracking-wide uppercase">
            {section.label}
          </p>
          <ul className="space-y-0.5">
            {section.items.map((item) => {
              const active =
                pathname === item.href.split("?")[0] ||
                pathname.startsWith(`${item.href.split("?")[0]}/`);
              return (
                <li key={`${item.href}:${item.label}`}>
                  <Link
                    href={item.href}
                    aria-current={active ? "page" : undefined}
                    className={cn(
                      "relative block rounded-md px-2.5 py-1.5 text-sm transition-colors",
                      active
                        ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                        : "text-sidebar-foreground/75 hover:bg-sidebar-accent/50 hover:text-sidebar-foreground",
                    )}
                  >
                    {active && (
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
      ))}
    </nav>
  );
}
