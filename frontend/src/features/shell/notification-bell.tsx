"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import {
  listNotifications,
  markNotificationRead,
  muteTrigger,
  type NotificationItem,
} from "@/features/notifications/api";
import { rememberMuted } from "@/features/notifications/muted-triggers-store";

function BellIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-4">
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M6 8a6 6 0 1 1 12 0c0 4 1.5 5.5 2 6H4c.5-.5 2-2 2-6Z"
      />
      <path strokeLinecap="round" strokeLinejoin="round" d="M10 19a2 2 0 0 0 4 0" />
    </svg>
  );
}

const SEVERITY_DOT: Record<string, string> = {
  Critical: "bg-negative",
  Warning: "bg-attention",
  Info: "bg-primary",
};

/** The BRD §66 notification centre's entry point — a bell with an unread badge and a quick feed. */
export function NotificationBell() {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const { data: notifications = [] } = useQuery({
    queryKey: ["notifications", { unreadOnly: false }],
    queryFn: () => listNotifications(false),
    refetchInterval: 60_000,
  });
  const unreadCount = notifications.filter((n) => !n.read).length;
  const recent = notifications.slice(0, 8);

  const markRead = useMutation({
    mutationFn: (id: number) => markNotificationRead(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["notifications"] }),
  });
  const mute = useMutation({
    mutationFn: (trigger: string) => muteTrigger(trigger),
    onSuccess: (_, trigger) => {
      rememberMuted(trigger);
      void queryClient.invalidateQueries({ queryKey: ["notifications"] });
    },
  });

  useEffect(() => {
    function onClickAway(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }
    document.addEventListener("mousedown", onClickAway);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onClickAway);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, []);

  return (
    <div ref={containerRef} className="relative">
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        aria-label={unreadCount > 0 ? `Notifications (${unreadCount} unread)` : "Notifications"}
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
      >
        <BellIcon />
        {unreadCount > 0 && (
          <span className="bg-negative absolute -top-0.5 -right-0.5 flex size-4 items-center justify-center rounded-full text-[10px] font-medium text-white">
            {unreadCount > 9 ? "9+" : unreadCount}
          </span>
        )}
      </Button>

      {open && (
        <div className="bg-popover border-border animate-in fade-in-0 zoom-in-95 slide-in-from-top-1 absolute right-0 z-40 mt-1 w-80 origin-top-right rounded-lg border shadow-lg duration-150">
          <div className="border-border flex items-center justify-between border-b px-3 py-2">
            <span className="text-sm font-semibold">Notifications</span>
            <Link
              href="/notifications"
              className="text-primary text-xs hover:underline"
              onClick={() => setOpen(false)}
            >
              View all
            </Link>
          </div>
          <div className="max-h-96 overflow-y-auto">
            {recent.length === 0 && (
              <p className="text-muted-foreground p-4 text-center text-sm">All caught up.</p>
            )}
            {recent.map((n: NotificationItem) => (
              <div
                key={n.id}
                className={`border-border flex gap-2 border-b p-3 text-sm last:border-0 ${n.read ? "" : "bg-secondary/40"}`}
              >
                <span
                  className={`mt-1 size-2 shrink-0 rounded-full ${SEVERITY_DOT[n.severity] ?? "bg-muted-foreground"}`}
                  aria-hidden="true"
                />
                <div className="min-w-0 flex-1">
                  <p className="font-medium">{n.title}</p>
                  <p className="text-muted-foreground line-clamp-2 text-xs">{n.body}</p>
                  <div className="mt-1 flex gap-3">
                    {!n.read && (
                      <button
                        type="button"
                        className="text-primary text-xs hover:underline"
                        onClick={() => markRead.mutate(n.id)}
                      >
                        Mark read
                      </button>
                    )}
                    <button
                      type="button"
                      className="text-muted-foreground text-xs hover:underline"
                      onClick={() => mute.mutate(n.trigger)}
                    >
                      Mute this type
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
