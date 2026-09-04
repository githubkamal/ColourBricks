"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  listNotifications,
  markNotificationRead,
  muteTrigger,
  unmuteTrigger,
  type NotificationItem,
} from "./api";
import { forgetMuted, getMutedTriggers, rememberMuted } from "./muted-triggers-store";

const SEVERITY_DOT: Record<string, string> = {
  Critical: "bg-negative",
  Warning: "bg-attention",
  Info: "bg-primary",
};

/** The full BRD §66 notification feed — every trigger, mark-read, and per-type muting. */
export function NotificationsPage() {
  const queryClient = useQueryClient();
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [muted, setMuted] = useState<string[]>(getMutedTriggers);

  const { data: notifications = [], isPending } = useQuery({
    queryKey: ["notifications", { unreadOnly }],
    queryFn: () => listNotifications(unreadOnly),
  });

  const markRead = useMutation({
    mutationFn: (id: number) => markNotificationRead(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["notifications"] }),
  });
  const mute = useMutation({
    mutationFn: (trigger: string) => muteTrigger(trigger),
    onSuccess: (_, trigger) => {
      rememberMuted(trigger);
      setMuted(getMutedTriggers());
      void queryClient.invalidateQueries({ queryKey: ["notifications"] });
    },
  });
  const unmute = useMutation({
    mutationFn: (trigger: string) => unmuteTrigger(trigger),
    onSuccess: (_, trigger) => {
      forgetMuted(trigger);
      setMuted(getMutedTriggers());
      void queryClient.invalidateQueries({ queryKey: ["notifications"] });
    },
  });

  return (
    <div className="max-w-3xl space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Notifications</h1>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={unreadOnly}
            onChange={(e) => setUnreadOnly(e.target.checked)}
          />
          Unread only
        </label>
      </div>

      <div className="rounded border">
        {isPending && <p className="text-muted-foreground p-4 text-sm">Loading…</p>}
        {!isPending && notifications.length === 0 && (
          <p className="text-muted-foreground p-4 text-sm">
            {unreadOnly ? "No unread notifications." : "No notifications yet."}
          </p>
        )}
        {notifications.map((n: NotificationItem) => (
          <div
            key={n.id}
            className={`flex gap-3 border-b p-4 text-sm last:border-0 ${n.read ? "" : "bg-secondary/40"}`}
          >
            <span
              className={`mt-1.5 size-2.5 shrink-0 rounded-full ${SEVERITY_DOT[n.severity] ?? "bg-muted-foreground"}`}
              aria-hidden="true"
            />
            <div className="min-w-0 flex-1 space-y-1">
              <div className="flex items-start justify-between gap-3">
                <p className="font-medium">{n.title}</p>
                <span className="text-muted-foreground shrink-0 text-xs whitespace-nowrap">
                  {new Date(n.createdAtUtc).toLocaleString()}
                </span>
              </div>
              <p className="text-muted-foreground">{n.body}</p>
              <div className="flex gap-3 pt-1">
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

      {muted.length > 0 && (
        <div className="rounded border p-4">
          <h2 className="mb-2 text-sm font-semibold">Muted notification types</h2>
          <ul className="space-y-1">
            {muted.map((trigger) => (
              <li key={trigger} className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">{trigger}</span>
                <button
                  type="button"
                  className="text-primary text-xs hover:underline"
                  onClick={() => unmute.mutate(trigger)}
                >
                  Unmute
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
