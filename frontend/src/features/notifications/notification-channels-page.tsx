"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import {
  evaluateNotifications,
  getChannelConfig,
  setChannelConfig,
  type NotificationChannelConfig,
} from "./api";

const CHANNELS: NotificationChannelConfig["channel"][] = ["Off", "Dashboard", "Email", "Both"];

/** Admin: which channel (BRD §66) each role gets for each of the ten notification triggers. */
export function NotificationChannelsPage() {
  const queryClient = useQueryClient();
  const { data: rows = [], isPending } = useQuery({
    queryKey: ["notification-config"],
    queryFn: getChannelConfig,
  });

  const update = useMutation({
    mutationFn: (input: { roleId: number; trigger: string; channel: NotificationChannelConfig["channel"] }) =>
      setChannelConfig(input.roleId, input.trigger, input.channel),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["notification-config"] }),
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the channel"),
  });

  const evaluate = useMutation({
    mutationFn: evaluateNotifications,
    onSuccess: (result) =>
      toast.success(
        `${result.created} new notification(s), ${result.emailsSent} email(s) sent` +
          (result.emailsFailed > 0 ? `, ${result.emailsFailed} failed` : ""),
      ),
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not run the evaluation"),
  });

  const roles = [...new Map(rows.map((r) => [r.roleId, r.roleName])).entries()];
  const triggers = [...new Set(rows.map((r) => r.trigger))];
  const cell = (roleId: number, trigger: string) =>
    rows.find((r) => r.roleId === roleId && r.trigger === trigger)?.channel ?? "Dashboard";

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold">Notification Channels</h1>
          <p className="text-muted-foreground text-sm">
            Which channel each role gets for each notification trigger (BRD §66). A disabled
            module never reaches a role, regardless of this setting.
          </p>
        </div>
        <Button type="button" variant="outline" onClick={() => evaluate.mutate()} disabled={evaluate.isPending}>
          Run evaluation now
        </Button>
      </div>

      {isPending && <p className="text-muted-foreground text-sm">Loading…</p>}

      {!isPending && (
        <div className="overflow-x-auto rounded border">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Trigger</th>
                {roles.map(([roleId, roleName]) => (
                  <th key={roleId} className="p-2 font-medium">
                    {roleName}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {triggers.map((trigger) => (
                <tr key={trigger} className="border-b last:border-0">
                  <td className="p-2 font-mono text-xs">{trigger}</td>
                  {roles.map(([roleId]) => (
                    <td key={roleId} className="p-2">
                      <select
                        className="rounded border bg-transparent px-2 py-1 text-xs"
                        aria-label={`${trigger} channel for role ${roleId}`}
                        value={cell(roleId, trigger)}
                        onChange={(e) =>
                          update.mutate({
                            roleId,
                            trigger,
                            channel: e.target.value as NotificationChannelConfig["channel"],
                          })
                        }
                      >
                        {CHANNELS.map((c) => (
                          <option key={c} value={c}>
                            {c}
                          </option>
                        ))}
                      </select>
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
