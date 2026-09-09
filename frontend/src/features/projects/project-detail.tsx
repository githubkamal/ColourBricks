"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { cn } from "@/lib/utils";
import { getProject, updateProject } from "./api";
import { ProjectTabs } from "./project-tabs";
import { PROJECT_STATUSES, PROJECT_STATUS_LABELS, type ProjectDetail as ProjectDetailDto } from "./types";

const schema = z
  .object({
    name: z.string().min(1, "Name is required").max(200),
    status: z.enum(PROJECT_STATUSES),
    managerId: z.string().optional().or(z.literal("")),
    startDate: z.string().min(1, "Start date is required"),
    expectedEndDate: z.string().optional().or(z.literal("")),
    contractValue: z.coerce
      .number()
      .refine((v) => !Number.isNaN(v), "Enter an amount")
      .refine((v) => v >= 0, "Cannot be negative"),
    estimatedCost: z.coerce
      .number()
      .refine((v) => !Number.isNaN(v), "Estimated cost is required")
      .refine((v) => v >= 0, "Cannot be negative"),
    siteAddress: z.string().max(500).optional().or(z.literal("")),
    contactDetails: z.string().optional().or(z.literal("")),
    notes: z.string().optional().or(z.literal("")),
  })
  .refine((v) => !v.expectedEndDate || v.expectedEndDate >= v.startDate, {
    path: ["expectedEndDate"],
    message: "Cannot be before the start date",
  });

type FormValues = z.input<typeof schema>;

const API_FIELD_TO_FORM: Record<string, keyof FormValues> = {
  name: "name",
  Name: "name",
  status: "status",
  Status: "status",
  EstimatedCost: "estimatedCost",
  ContractValue: "contractValue",
  ExpectedEndDate: "expectedEndDate",
};

export function ProjectDetail({ id }: { id: number }) {
  const [editing, setEditing] = useState(false);
  const {
    data: project,
    isPending,
    isError,
  } = useQuery({
    queryKey: ["project", id],
    queryFn: () => getProject(id),
  });

  if (isPending || isError || !project) {
    return (
      <div className="max-w-5xl space-y-5">
        <ProjectTabs projectId={id} title="Overview" />
        {dataState({ isPending, isError: isError || !project, errorLabel: "Project not found." })}
      </div>
    );
  }

  return (
    <div className="max-w-5xl space-y-5">
      <ProjectTabs projectId={id} title="Overview" />

      <div className="bg-card border-border rounded-xl border p-5 shadow-xs">
        {editing ? (
          <ProjectOverviewForm project={project} onDone={() => setEditing(false)} />
        ) : (
          <>
            <div className="flex justify-end">
              <Button type="button" variant="outline" size="sm" onClick={() => setEditing(true)}>
                Edit
              </Button>
            </div>
            <dl className="mt-3 grid grid-cols-1 gap-x-8 gap-y-4 text-sm sm:grid-cols-2">
              <Row label="Manager">{project.managerId ? `#${project.managerId}` : "—"}</Row>
              <Row label="Status">{project.status}</Row>
              <Row label="Start date">{formatDate(project.startDate)}</Row>
              <Row label="Expected completion">
                {project.expectedEndDate ? formatDate(project.expectedEndDate) : "—"}
              </Row>
              <Row label="Contract value" numeric>
                {formatINR(project.contractValue)}
              </Row>
              <Row label="Estimated cost" numeric>
                {formatINR(project.estimatedCost)}
              </Row>
              <Row label="Site address" className="sm:col-span-2">
                {project.siteAddress ?? "—"}
              </Row>
              <Row label="Contact details" className="sm:col-span-2">
                {project.contactDetails ?? "—"}
              </Row>
              <Row label="Notes" className="sm:col-span-2">
                {project.notes ?? "—"}
              </Row>
            </dl>
          </>
        )}
      </div>
    </div>
  );
}

function ProjectOverviewForm({
  project,
  onDone,
}: {
  project: ProjectDetailDto;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: project.name,
      status: project.status,
      managerId: project.managerId ? String(project.managerId) : "",
      startDate: project.startDate,
      expectedEndDate: project.expectedEndDate ?? "",
      contractValue: project.contractValue,
      estimatedCost: project.estimatedCost,
      siteAddress: project.siteAddress ?? "",
      contactDetails: project.contactDetails ?? "",
      notes: project.notes ?? "",
    },
  });

  useEffect(() => {
    const first = Object.keys(errors)[0];
    if (first) setFocus(first as keyof FormValues);
  }, [errors, setFocus]);

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      updateProject(project.id, {
        name: values.name,
        clientId: project.clientId,
        managerId: values.managerId ? Number(values.managerId) : null,
        status: values.status,
        startDate: values.startDate,
        expectedEndDate: values.expectedEndDate ? values.expectedEndDate : null,
        actualEndDate: project.actualEndDate,
        contractValue: Number(values.contractValue),
        estimatedCost: Number(values.estimatedCost),
        siteAddress: values.siteAddress ? values.siteAddress : null,
        contactDetails: values.contactDetails ? values.contactDetails : null,
        notes: values.notes ? values.notes : null,
        concurrencyStamp: project.concurrencyStamp,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["project", project.id] });
      await queryClient.invalidateQueries({ queryKey: ["projects"] });
      toast.success("Project updated");
      onDone();
    },
    onError: (error) => {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          const formField = API_FIELD_TO_FORM[field];
          if (formField) setError(formField, { message: messages[0] });
        }
        toast.error("Please fix the highlighted fields");
      } else if (error instanceof ApiError && error.status === 409) {
        toast.error("This project changed elsewhere — reload and try again.");
      } else {
        toast.error(error instanceof ApiError ? error.message : "Could not update the project");
      }
    },
  });

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-5" noValidate>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label="Name" error={errors.name?.message} className="sm:col-span-2">
          <Input autoFocus autoComplete="off" {...register("name")} />
        </Field>

        <Field label="Status" error={errors.status?.message}>
          <Select {...register("status")} className="w-full">
            {PROJECT_STATUSES.map((s) => (
              <option key={s} value={s}>
                {PROJECT_STATUS_LABELS[s]}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Manager ID" error={errors.managerId?.message}>
          <Input inputMode="numeric" {...register("managerId")} />
        </Field>

        <Field label="Start date" error={errors.startDate?.message}>
          <Input type="date" {...register("startDate")} />
        </Field>

        <Field label="Expected completion" error={errors.expectedEndDate?.message}>
          <Input type="date" {...register("expectedEndDate")} />
        </Field>

        <Field label="Contract value (₹)" error={errors.contractValue?.message}>
          <Input type="number" step="0.01" min="0" className="num" {...register("contractValue")} />
        </Field>

        <Field label="Estimated cost (₹)" error={errors.estimatedCost?.message}>
          <Input type="number" step="0.01" min="0" className="num" {...register("estimatedCost")} />
        </Field>

        <Field label="Site address" error={errors.siteAddress?.message} className="sm:col-span-2">
          <Input autoComplete="off" {...register("siteAddress")} />
        </Field>

        <Field label="Contact details" error={errors.contactDetails?.message} className="sm:col-span-2">
          <Input autoComplete="off" {...register("contactDetails")} />
        </Field>

        <Field label="Notes" error={errors.notes?.message} className="sm:col-span-2">
          <textarea
            {...register("notes")}
            rows={3}
            className="border-input bg-card w-full rounded border px-3 py-2 text-sm"
          />
        </Field>
      </div>

      <div className="flex gap-2">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? "Saving…" : "Save changes"}
        </Button>
        <Button type="button" variant="outline" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function Field({
  label,
  error,
  className,
  children,
}: {
  label: string;
  error?: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <label className={cn("block space-y-1", className)}>
      <span className="text-sm font-medium">{label}</span>
      {children}
      {error && <p className="text-negative text-xs">{error}</p>}
    </label>
  );
}

function Row({
  label,
  children,
  numeric,
  className,
}: {
  label: string;
  children: React.ReactNode;
  numeric?: boolean;
  className?: string;
}) {
  return (
    <div className={className}>
      <dt className="text-muted-foreground text-xs">{label}</dt>
      <dd className={numeric ? "tabular-nums" : undefined}>{children}</dd>
    </div>
  );
}
