"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useEffect } from "react";
import { useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { SubmitButton } from "@/components/ui/submit-button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { cn } from "@/lib/utils";
import { createProject, findProjectDuplicates } from "./api";
import { PROJECT_STATUSES, PROJECT_STATUS_LABELS } from "./types";

const schema = z
  .object({
    name: z.string().min(1, "Name is required").max(200),
    code: z
      .string()
      .max(30)
      .regex(/^[A-Za-z0-9-]*$/, "Letters, digits and hyphens only")
      .optional()
      .or(z.literal("")),
    status: z.enum(PROJECT_STATUSES),
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
    notes: z.string().optional().or(z.literal("")),
  })
  .refine((v) => !v.expectedEndDate || v.expectedEndDate >= v.startDate, {
    path: ["expectedEndDate"],
    message: "Cannot be before the start date",
  });

type FormValues = z.input<typeof schema>;

const API_FIELD_TO_FORM: Record<string, keyof FormValues> = {
  code: "code",
  Code: "code",
  name: "name",
  Name: "name",
  EstimatedCost: "estimatedCost",
  ContractValue: "contractValue",
  ExpectedEndDate: "expectedEndDate",
};

export function ProjectForm() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();

  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    control,
    formState: { errors, isDirty, isSubmitSuccessful },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { status: "Ongoing" },
  });

  // "This site already exists" check — runs as the name / site address are typed, so
  // the warning shows before the user fills in the rest of the form.
  const debouncedName = useDebouncedValue(useWatch({ control, name: "name" }) ?? "", 400);
  const debouncedSite = useDebouncedValue(useWatch({ control, name: "siteAddress" }) ?? "", 400);
  const { data: duplicates = [] } = useQuery({
    queryKey: ["project-duplicates", debouncedName, debouncedSite],
    queryFn: () => findProjectDuplicates(debouncedName, debouncedSite),
    enabled: debouncedName.trim() !== "" || debouncedSite.trim() !== "",
  });

  useEffect(() => {
    const first = Object.keys(errors)[0];
    if (first) setFocus(first as keyof FormValues);
  }, [errors, setFocus]);

  useEffect(() => {
    if (!isDirty || isSubmitSuccessful) return;
    function handler(e: BeforeUnloadEvent) {
      e.preventDefault();
    }
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [isDirty, isSubmitSuccessful]);

  async function handleCancel() {
    if (isDirty) {
      const { confirmed } = await confirm({
        title: "Discard this project?",
        description: "Your changes have not been saved.",
        confirmLabel: "Discard",
        destructive: true,
      });
      if (!confirmed) return;
    }
    router.back();
  }

  const mutation = useMutation({
    mutationFn: createProject,
    onSuccess: async (project) => {
      await queryClient.invalidateQueries({ queryKey: ["projects"] });
      toast.success(`Project ${project.code} created`);
      router.push(`/projects/${project.id}`);
    },
    onError: (error) => {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          const formField = API_FIELD_TO_FORM[field];
          if (formField) setError(formField, { message: messages[0] });
        }
        toast.error("Please fix the highlighted fields");
      } else {
        toast.error(error instanceof ApiError ? error.message : "Could not create the project");
      }
    },
  });

  async function onSubmit(values: FormValues) {
    // Re-check at submit time: the typed-ahead query may be stale or still debouncing.
    let found = duplicates;
    try {
      found = await findProjectDuplicates(values.name, values.siteAddress ?? "");
    } catch {
      // Fall back to the server-side guard, which rejects an unconfirmed duplicate.
    }
    if (found.length > 0) {
      const { confirmed } = await confirm({
        title: "This site already exists",
        description: `${found
          .map((d) => `${d.code} — ${d.name}`)
          .join("; ")}. Create another project anyway?`,
        confirmLabel: "Create anyway",
      });
      if (!confirmed) return;
    }
    mutation.mutate({
      confirmDuplicate: found.length > 0,
      name: values.name,
      code: values.code ? values.code : null,
      status: values.status,
      startDate: values.startDate,
      expectedEndDate: values.expectedEndDate ? values.expectedEndDate : null,
      contractValue: Number(values.contractValue),
      estimatedCost: Number(values.estimatedCost),
      siteAddress: values.siteAddress ? values.siteAddress : null,
      notes: values.notes ? values.notes : null,
    });
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="max-w-2xl space-y-5" noValidate>
      {dialog}
      <h1 className="text-lg font-semibold">New project</h1>

      {duplicates.length > 0 && (
        <div
          role="alert"
          className="border-attention/50 bg-attention/10 space-y-1 rounded-lg border p-3 text-sm"
        >
          <p className="font-medium">
            A project with this {duplicates.some((d) => d.matchedOn === "name") ? "name" : "site address"}{" "}
            already exists. Check before creating a duplicate site:
          </p>
          <ul className="list-inside list-disc">
            {duplicates.map((d) => (
              <li key={d.id}>
                <Link href={`/projects/${d.id}`} target="_blank" className="underline">
                  {d.code} — {d.name}
                </Link>
                {d.siteAddress ? <span className="text-muted-foreground"> · {d.siteAddress}</span> : null}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label="Name" error={errors.name?.message} className="sm:col-span-2">
          <Input autoFocus autoComplete="off" {...register("name")} />
        </Field>

        <Field label="Code (optional — auto-generated)" error={errors.code?.message}>
          <Input placeholder="CB-2026-001" {...register("code")} />
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

        <Field label="Notes" error={errors.notes?.message} className="sm:col-span-2">
          <textarea
            {...register("notes")}
            rows={3}
            className="border-input bg-card w-full rounded border px-3 py-2 text-sm"
          />
        </Field>
      </div>

      <div className="flex gap-2">
        <SubmitButton type="submit" mutation={mutation}>
          {mutation.isPending ? "Creating…" : "Create project"}
        </SubmitButton>
        <Button type="button" variant="outline" onClick={handleCancel}>
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
