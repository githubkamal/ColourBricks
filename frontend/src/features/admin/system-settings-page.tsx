"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { attachmentPreviewUrl, uploadAttachment } from "@/features/attachments/api";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { FieldLabel } from "@/components/ui/field-label";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { getSystemSettings, updateSystemSettings, type SystemSettings } from "./system-settings-api";

/** Admin > System Settings: company profile + notification-threshold defaults (client-confirmed scope, 2026-09-04). */
export function SystemSettingsPage() {
  const { data, isPending } = useQuery({ queryKey: ["system-settings"], queryFn: getSystemSettings });

  if (isPending || !data) {
    return <p className="text-muted-foreground text-sm">Loading…</p>;
  }

  // Remounted (via `key`) whenever the server's copy changes — e.g. after a save,
  // or a reload post-conflict — so the form's local state is always seeded fresh
  // from the fetched row without a state-sync effect.
  return <SettingsForm key={data.concurrencyStamp} initial={data} />;
}

function SettingsForm({ initial }: { initial: SystemSettings }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<SystemSettings>(initial);
  const [touched, setTouched] = useState(false);
  const logoInputRef = useRef<HTMLInputElement>(null);

  const uploadLogo = useMutation({
    mutationFn: (file: File) => uploadAttachment("SystemSettings", form.id, file),
    onSuccess: (attachment) => {
      // Staged like every other field here — only takes effect once "Save
      // settings" is submitted, even though the file itself is already stored.
      setForm((f) => ({ ...f, companyLogoAttachmentId: attachment.id, companyLogoUrl: null }));
      toast.message("Logo uploaded — click Save settings to apply it");
      if (logoInputRef.current) logoInputRef.current.value = "";
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not upload the logo"),
  });

  const save = useMutation({
    mutationFn: (input: SystemSettings) => updateSystemSettings(input),
    onSuccess: (result) => {
      toast.success("Settings saved");
      queryClient.setQueryData(["system-settings"], result);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        toast.error("Someone else changed these settings — reload and try again.");
        void queryClient.invalidateQueries({ queryKey: ["system-settings"] });
        return;
      }
      toast.error(error instanceof ApiError ? error.message : "Could not save settings");
    },
  });

  const ready = form.companyName.trim() !== "";

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-lg font-semibold">System Settings</h1>

      <form
        className="space-y-6"
        onSubmit={(e) => {
          e.preventDefault();
          setTouched(true);
          if (ready) save.mutate(form);
        }}
      >
        <section className="space-y-3 rounded border p-4">
          <h2 className="text-sm font-semibold">Company profile</h2>
          <p className="text-muted-foreground text-xs">
            Shown on report headers, exports and printed documents.
          </p>

          <label className="block space-y-1">
            <FieldLabel required error={touched && !ready ? "Required" : undefined}>
              Company name
            </FieldLabel>
            <Input
              value={form.companyName}
              aria-label="Company name"
              onChange={(e) => setForm({ ...form, companyName: e.target.value })}
            />
          </label>
          <label className="block space-y-1">
            <span className="text-sm font-medium">Address</span>
            <Input
              value={form.companyAddress ?? ""}
              aria-label="Address"
              onChange={(e) => setForm({ ...form, companyAddress: e.target.value })}
            />
          </label>
          <label className="block space-y-1">
            <span className="text-sm font-medium">GSTIN</span>
            <Input
              value={form.companyGstin ?? ""}
              aria-label="GSTIN"
              onChange={(e) => setForm({ ...form, companyGstin: e.target.value })}
            />
          </label>

          <div className="space-y-2">
            <span className="text-sm font-medium">Logo</span>
            {form.companyLogoAttachmentId ? (
              <div className="flex items-center gap-3">
                {/* eslint-disable-next-line @next/next/no-img-element -- a settings-page preview, not a next/image candidate */}
                <img
                  src={attachmentPreviewUrl(form.companyLogoAttachmentId)}
                  alt="Company logo"
                  className="h-12 w-auto rounded border object-contain"
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="xs"
                  onClick={() => setForm({ ...form, companyLogoAttachmentId: null })}
                >
                  Remove
                </Button>
              </div>
            ) : form.companyLogoUrl ? (
              <div className="flex items-center gap-3">
                {/* eslint-disable-next-line @next/next/no-img-element -- an externally hosted URL, next/image can't optimise an arbitrary host anyway */}
                <img
                  src={form.companyLogoUrl}
                  alt="Company logo"
                  className="h-12 w-auto rounded border object-contain"
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="xs"
                  onClick={() => setForm({ ...form, companyLogoUrl: null })}
                >
                  Remove
                </Button>
              </div>
            ) : (
              <div className="flex flex-wrap items-center gap-3">
                <input
                  ref={logoInputRef}
                  type="file"
                  aria-label="Upload a logo"
                  accept="image/png,image/jpeg,image/webp,image/svg+xml"
                  className="text-xs"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file) uploadLogo.mutate(file);
                  }}
                />
                <span className="text-muted-foreground text-xs">or</span>
                <Input
                  className="max-w-xs"
                  aria-label="Logo URL"
                  placeholder="paste a hosted image URL"
                  onBlur={(e) => {
                    if (e.target.value.trim()) setForm({ ...form, companyLogoUrl: e.target.value.trim() });
                  }}
                />
              </div>
            )}
          </div>
        </section>

        <section className="space-y-3 rounded border p-4">
          <h2 className="text-sm font-semibold">Notification defaults</h2>
          <p className="text-muted-foreground text-xs">
            Thresholds the daily notification job and loan EMI alerts use.
          </p>

          <div className="grid grid-cols-2 gap-3">
            <label className="space-y-1">
              <span className="text-sm font-medium">Vendor outstanding alert limit</span>
              <AmountInput
                value={String(form.vendorOutstandingAlertLimit)}
                aria-label="Vendor outstanding alert limit"
                onChange={(v) => setForm({ ...form, vendorOutstandingAlertLimit: Number(v) || 0 })}
              />
            </label>
            <label className="space-y-1">
              <span className="text-sm font-medium">Overdue alert (days)</span>
              <Input
                inputMode="numeric"
                value={form.overdueAlertDays}
                aria-label="Overdue alert days"
                onChange={(e) =>
                  setForm({ ...form, overdueAlertDays: Number(e.target.value.replace(/\D/g, "")) || 0 })
                }
              />
            </label>
            <label className="space-y-1">
              <span className="text-sm font-medium">Profit floor alert %</span>
              <AmountInput
                value={String(form.profitFloorAlertPercent)}
                aria-label="Profit floor alert percent"
                onChange={(v) => setForm({ ...form, profitFloorAlertPercent: Number(v) || 0 })}
              />
            </label>
            <label className="space-y-1">
              <span className="text-sm font-medium">Loan EMI reminder (days ahead)</span>
              <Input
                inputMode="numeric"
                value={form.loanEmiReminderDaysAhead}
                aria-label="Loan EMI reminder days ahead"
                onChange={(e) =>
                  setForm({
                    ...form,
                    loanEmiReminderDaysAhead: Number(e.target.value.replace(/\D/g, "")) || 0,
                  })
                }
              />
            </label>
          </div>
        </section>

        <Button type="submit" disabled={!ready || save.isPending}>
          Save settings
        </Button>
      </form>
    </div>
  );
}
