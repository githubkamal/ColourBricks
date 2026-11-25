"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { SubmitButton } from "@/components/ui/submit-button";
import { listDepartments } from "@/features/departments/api";
import { ProjectPicker, type ProjectPickerSelection } from "@/features/projects/project-picker";
import { ApiError } from "@/lib/api";
import { formatINR } from "@/lib/format";
import { getTeam, updateTeam } from "./api";
import type { TeamDto } from "./types";

interface SiteRow {
  key: number;
  project: ProjectPickerSelection | null;
  siteLocation: string;
  contractValue: string;
}

interface FormState {
  name: string;
  departmentId: number | "";
  isActive: boolean;
  contactPerson: string;
  phone: string;
  alternatePhone: string;
  email: string;
  workingArea: string;
  address: string;
  paymentTerms: string;
  bankAccountHolderName: string;
  bankName: string;
  bankAccountNumber: string;
  bankIfsc: string;
  sites: SiteRow[];
}

let nextKey = 1;

function toForm(team: TeamDto): FormState {
  return {
    name: team.name,
    departmentId: team.departmentId ?? "",
    isActive: team.isActive,
    contactPerson: team.contactPerson ?? "",
    phone: team.phone ?? "",
    alternatePhone: team.alternatePhone ?? "",
    email: team.email ?? "",
    workingArea: team.workingArea ?? "",
    address: team.address ?? "",
    paymentTerms: team.paymentTerms ?? "",
    bankAccountHolderName: team.bankAccountHolderName ?? "",
    bankName: team.bankName ?? "",
    bankAccountNumber: team.bankAccountNumber ?? "",
    bankIfsc: team.bankIfsc ?? "",
    sites: (team.sites ?? []).map((s) => ({
      key: nextKey++,
      project: s.projectId ? { id: s.projectId, name: s.projectName ?? "" } : null,
      siteLocation: s.siteLocation ?? "",
      contractValue: String(s.contractValue),
    })),
  };
}

const PHONE = /^[0-9+\-\s()]{0,20}$/;
const IFSC = /^[A-Za-z]{4}0[A-Za-z0-9]{6}$/;

function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === "" ? null : trimmed;
}

/**
 * Team master (client request, 2026-11-21): contact, area, sites with contract
 * value, address and bank account details for one subcontractor team.
 */
export function TeamMasterForm({
  teamId,
  onSaved,
}: {
  teamId: number;
  onSaved?: (team: TeamDto) => void;
}) {
  const { data: team, isPending } = useQuery({
    queryKey: ["team", teamId],
    queryFn: () => getTeam(teamId),
  });

  if (isPending || !team) {
    return <p className="text-muted-foreground text-sm">Loading team details…</p>;
  }

  // Keyed on the stamp so a save (or a refetch) resets the form to the saved values.
  return <TeamMasterFields key={team.concurrencyStamp} team={team} onSaved={onSaved} />;
}

function TeamMasterFields({ team, onSaved }: { team: TeamDto; onSaved?: (team: TeamDto) => void }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<FormState>(() => toForm(team));

  const { data: departments = [] } = useQuery({
    queryKey: ["departments", { includeInactive: false }],
    queryFn: () => listDepartments(false),
  });

  function set<K extends keyof FormState>(field: K, value: FormState[K]) {
    setForm((f) => ({ ...f, [field]: value }));
  }

  function setSite(key: number, patch: Partial<SiteRow>) {
    setForm((f) => ({
      ...f,
      sites: f.sites.map((s) => (s.key === key ? { ...s, ...patch } : s)),
    }));
  }

  const errors = {
    phone: PHONE.test(form.phone) ? undefined : "Use digits only",
    alternatePhone: PHONE.test(form.alternatePhone) ? undefined : "Use digits only",
    bankIfsc:
      form.bankIfsc.trim() === "" || IFSC.test(form.bankIfsc.trim())
        ? undefined
        : "11 characters, e.g. SBIN0001234",
    sites: form.sites.some((s) => !s.project && s.siteLocation.trim() === "")
      ? "Each site needs a project or a location"
      : undefined,
  };
  const valid =
    form.name.trim() !== "" && form.departmentId !== "" && Object.values(errors).every((e) => !e);

  const totalContract = form.sites.reduce((sum, s) => sum + (Number(s.contractValue) || 0), 0);

  const save = useMutation({
    mutationFn: () =>
      updateTeam(team.id, {
        name: form.name.trim(),
        departmentId: Number(form.departmentId),
        isActive: form.isActive,
        concurrencyStamp: team.concurrencyStamp,
        contactPerson: orNull(form.contactPerson),
        phone: orNull(form.phone),
        alternatePhone: orNull(form.alternatePhone),
        email: orNull(form.email),
        address: orNull(form.address),
        workingArea: orNull(form.workingArea),
        paymentTerms: orNull(form.paymentTerms),
        bankDetails: team.bankDetails,
        bankAccountHolderName: orNull(form.bankAccountHolderName),
        bankName: orNull(form.bankName),
        bankAccountNumber: orNull(form.bankAccountNumber),
        bankIfsc: orNull(form.bankIfsc)?.toUpperCase() ?? null,
        sites: form.sites.map((s) => ({
          projectId: s.project?.id ?? null,
          siteLocation: orNull(s.siteLocation),
          contractValue: Number(s.contractValue) || 0,
        })),
      }),
    onSuccess: (saved) => {
      toast.success(`${saved.name} saved`);
      queryClient.setQueryData(["team", saved.id], saved);
      void queryClient.invalidateQueries({ queryKey: ["teams"] });
      onSaved?.(saved);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        toast.error(
          "Someone else changed this team, or the name is already used. Reload and try again.",
        );
        void queryClient.invalidateQueries({ queryKey: ["team", team.id] });
        return;
      }
      toast.error(error instanceof ApiError ? error.message : "Could not save the team");
    },
  });

  return (
    <form
      className="bg-card border-border space-y-5 rounded-xl border p-4 shadow-xs"
      onSubmit={(e) => {
        e.preventDefault();
        if (valid) save.mutate();
      }}
    >
      <Section title="Team">
        <Field label="Team name">
          <Input
            value={form.name}
            onChange={(e) => set("name", e.target.value)}
            aria-label="Team name"
          />
        </Field>
        <Field label="Department">
          <Select
            className="w-full"
            value={form.departmentId}
            aria-label="Team department"
            onChange={(e) => set("departmentId", e.target.value ? Number(e.target.value) : "")}
          >
            <option value="">Select…</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
        </Field>
        <label className="flex items-center gap-2 text-sm sm:col-span-2">
          <input
            type="checkbox"
            checked={form.isActive}
            onChange={(e) => set("isActive", e.target.checked)}
          />
          Active
        </label>
      </Section>

      <Section title="Contact details">
        <Field label="Contact person">
          <Input
            value={form.contactPerson}
            onChange={(e) => set("contactPerson", e.target.value)}
            aria-label="Contact person"
          />
        </Field>
        <Field label="Email">
          <Input
            type="email"
            value={form.email}
            onChange={(e) => set("email", e.target.value)}
            aria-label="Email"
          />
        </Field>
        <Field label="Mobile number" error={errors.phone}>
          <Input
            type="tel"
            inputMode="tel"
            value={form.phone}
            onChange={(e) => set("phone", e.target.value)}
            aria-label="Mobile number"
          />
        </Field>
        <Field label="Alternate contact number" error={errors.alternatePhone}>
          <Input
            type="tel"
            inputMode="tel"
            value={form.alternatePhone}
            onChange={(e) => set("alternatePhone", e.target.value)}
            aria-label="Alternate contact number"
          />
        </Field>
      </Section>

      <Section title="Area details">
        <Field label="Working area" className="sm:col-span-2">
          <Input
            value={form.workingArea}
            onChange={(e) => set("workingArea", e.target.value)}
            placeholder="e.g. Coimbatore North, Tiruppur"
            aria-label="Working area"
          />
        </Field>
      </Section>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">Sites &amp; contract value</h3>
          {form.sites.length > 0 && (
            <span className="text-muted-foreground text-sm tabular-nums">
              {form.sites.length} {form.sites.length === 1 ? "site" : "sites"} · Total{" "}
              {formatINR(totalContract)}
            </span>
          )}
        </div>
        {form.sites.length === 0 && (
          <p className="text-muted-foreground text-sm">No sites added yet.</p>
        )}
        {form.sites.map((site, index) => (
          <div
            key={site.key}
            className="border-border grid grid-cols-1 items-end gap-2 rounded-lg border p-2 sm:grid-cols-[1fr_1fr_10rem_auto]"
          >
            <ProjectPicker
              selected={site.project}
              onSelect={(p) => setSite(site.key, { project: p })}
              label={index === 0 ? "Project (site)" : undefined}
              ariaLabel={`Site ${index + 1} project`}
            />
            <Field label={index === 0 ? "Site location" : undefined}>
              <Input
                value={site.siteLocation}
                onChange={(e) => setSite(site.key, { siteLocation: e.target.value })}
                aria-label={`Site ${index + 1} location`}
              />
            </Field>
            <Field label={index === 0 ? "Contract value" : undefined}>
              <AmountInput
                currency
                value={site.contractValue}
                onChange={(v) => setSite(site.key, { contractValue: v })}
                aria-label={`Site ${index + 1} contract value`}
              />
            </Field>
            <Button
              type="button"
              size="xs"
              variant="ghost"
              className="text-destructive"
              onClick={() =>
                set(
                  "sites",
                  form.sites.filter((s) => s.key !== site.key),
                )
              }
            >
              Remove
            </Button>
          </div>
        ))}
        {errors.sites && <p className="text-destructive text-xs">{errors.sites}</p>}
        <Button
          type="button"
          size="xs"
          variant="outline"
          onClick={() =>
            set("sites", [
              ...form.sites,
              { key: nextKey++, project: null, siteLocation: "", contractValue: "" },
            ])
          }
        >
          + Add site
        </Button>
      </div>

      <Section title="Address details">
        <Field label="Residential / business address" className="sm:col-span-2">
          <textarea
            value={form.address}
            onChange={(e) => set("address", e.target.value)}
            rows={3}
            maxLength={500}
            aria-label="Address"
            className="border-input bg-card w-full rounded-md border px-3 py-2 text-sm"
          />
        </Field>
      </Section>

      <Section title="Bank account details">
        <Field label="Account holder name">
          <Input
            value={form.bankAccountHolderName}
            onChange={(e) => set("bankAccountHolderName", e.target.value)}
            aria-label="Account holder name"
          />
        </Field>
        <Field label="Bank name">
          <Input
            value={form.bankName}
            onChange={(e) => set("bankName", e.target.value)}
            aria-label="Bank name"
          />
        </Field>
        <Field label="Account number">
          <Input
            inputMode="numeric"
            value={form.bankAccountNumber}
            onChange={(e) => set("bankAccountNumber", e.target.value.replace(/[^0-9A-Za-z]/g, ""))}
            aria-label="Account number"
          />
        </Field>
        <Field label="IFSC code" error={errors.bankIfsc}>
          <Input
            value={form.bankIfsc}
            maxLength={11}
            onChange={(e) => set("bankIfsc", e.target.value.toUpperCase())}
            aria-label="IFSC code"
          />
        </Field>
        {team.bankDetails && (
          <p className="text-muted-foreground text-xs sm:col-span-2">
            Earlier notes: {team.bankDetails}
          </p>
        )}
      </Section>

      <Section title="Other">
        <Field label="Payment terms" className="sm:col-span-2">
          <Input
            value={form.paymentTerms}
            onChange={(e) => set("paymentTerms", e.target.value)}
            aria-label="Payment terms"
          />
        </Field>
      </Section>

      <SubmitButton type="submit" disabled={!valid} mutation={save}>
        Save team details
      </SubmitButton>
    </form>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <fieldset className="space-y-2">
      <legend className="mb-2 text-sm font-semibold">{title}</legend>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">{children}</div>
    </fieldset>
  );
}

function Field({
  label,
  error,
  className,
  children,
}: {
  label?: string;
  error?: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <label className={`block space-y-1 ${className ?? ""}`}>
      {label && <span className="text-sm font-medium">{label}</span>}
      {children}
      {error && <span className="text-destructive block text-xs">{error}</span>}
    </label>
  );
}
