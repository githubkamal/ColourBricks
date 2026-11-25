"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SubmitButton } from "@/components/ui/submit-button";
import { ProjectPicker, type ProjectPickerSelection } from "@/features/projects/project-picker";
import { TeamPicker } from "@/features/teams/team-picker";
import type { TeamDto } from "@/features/teams/types";
import { ApiError } from "@/lib/api";
import { formatINR } from "@/lib/format";
import { recordWork, updateWork, type WorkEntry } from "./api";

export interface WorkEntryFormProps {
  /** The entry being edited, or null to record a new one. */
  entry: WorkEntry | null;
  /** Project to start a new entry with (e.g. from `?projectId=`). */
  defaultProject?: ProjectPickerSelection | null;
  onSaved: (entry: WorkEntry) => void;
  onCancel?: () => void;
}

/**
 * Record or edit a labour / subcontractor work entry. Field order per the client's
 * layout request (2026-11-21): Team first, then Work date → Project → Agreed value.
 * Remount (via `key`) to load a different entry.
 */
export function WorkEntryForm({
  entry,
  defaultProject = null,
  onSaved,
  onCancel,
}: WorkEntryFormProps) {
  const [team, setTeam] = useState<TeamDto | null>(
    entry ? ({ id: entry.teamId, name: entry.teamName } as TeamDto) : null,
  );
  const [project, setProject] = useState<ProjectPickerSelection | null>(
    entry
      ? {
          id: entry.projectId,
          name: entry.projectName ?? `Project #${entry.projectId}`,
          code: entry.projectCode ?? undefined,
        }
      : defaultProject,
  );
  const [date, setDate] = useState(entry?.date ?? "");
  const [agreedValue, setAgreedValue] = useState(entry ? String(entry.agreedValue) : "");
  const [workType, setWorkType] = useState(entry?.workType ?? "");
  const [description, setDescription] = useState(entry?.description ?? "");

  // Payments carry the project and team in the books, so those stay fixed once paid.
  const locked = entry !== null && entry.totalPaid > 0;
  const belowPaid = locked && Number(agreedValue) < entry.totalPaid;

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        projectId: project!.id,
        teamId: team!.id,
        date,
        agreedValue: Number(agreedValue),
        workType: workType.trim() || null,
        description: description.trim() || null,
      };
      return entry ? updateWork(entry.id, payload) : recordWork(payload);
    },
    onSuccess: (saved) => {
      toast.success(
        entry
          ? `${saved.referenceNumber ?? "Work entry"} updated`
          : `Work entry ${saved.referenceNumber ?? ""} recorded`.replace(/\s+/g, " "),
      );
      if (!entry) {
        setAgreedValue("");
        setWorkType("");
        setDescription("");
      }
      onSaved(saved);
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError
          ? (Object.values(error.fieldErrors ?? {})[0]?.[0] ?? error.message)
          : "Could not save the work entry",
      ),
  });

  const ready =
    team !== null && project !== null && date !== "" && Number(agreedValue) > 0 && !belowPaid;

  return (
    <form
      className="bg-card border-border space-y-3 rounded-xl border p-4 shadow-xs"
      onSubmit={(e) => {
        e.preventDefault();
        if (ready) save.mutate();
      }}
    >
      {entry && (
        <p className="text-sm">
          Editing{" "}
          <span className="font-medium">{entry.referenceNumber ?? `entry #${entry.id}`}</span>
          {locked && (
            <span className="text-muted-foreground">
              {" "}
              · {formatINR(entry.totalPaid)} already paid, so the team and project can&apos;t change
            </span>
          )}
        </p>
      )}

      {locked ? (
        <div className="space-y-1">
          <span className="text-sm font-medium">Team</span>
          <p className="border-input bg-secondary/40 rounded-md border px-3 py-2 text-sm">
            {team?.name}
          </p>
        </div>
      ) : (
        <TeamPicker selected={team} onSelect={setTeam} label="Team" />
      )}

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[10rem_minmax(0,1fr)_11rem]">
        <label className="block space-y-1">
          <span className="text-sm font-medium">Work date</span>
          <Input
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            aria-label="Work date"
          />
        </label>
        {locked ? (
          <div className="space-y-1">
            <span className="text-sm font-medium">Project</span>
            <p className="border-input bg-secondary/40 truncate rounded-md border px-3 py-2 text-sm">
              {project?.code ? `${project.code} — ` : ""}
              {project?.name}
            </p>
          </div>
        ) : (
          <ProjectPicker selected={project} onSelect={setProject} label="Project" />
        )}
        <label className="block space-y-1">
          <span className="text-sm font-medium">Agreed work value</span>
          <AmountInput
            currency
            value={agreedValue}
            onChange={setAgreedValue}
            placeholder="0.00"
            aria-label="Agreed work value"
            error={belowPaid ? `Can't be below ${formatINR(entry!.totalPaid)} paid` : undefined}
          />
          {Number(agreedValue) > 0 && !belowPaid && (
            <span className="text-muted-foreground block text-xs tabular-nums">
              {formatINR(Number(agreedValue))}
            </span>
          )}
        </label>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[14rem_minmax(0,1fr)]">
        <label className="block space-y-1">
          <span className="text-sm font-medium">Work type</span>
          <Input
            value={workType}
            maxLength={80}
            onChange={(e) => setWorkType(e.target.value)}
            aria-label="Work type"
          />
        </label>
        <label className="block space-y-1">
          <span className="text-sm font-medium">Description</span>
          <Input
            value={description}
            maxLength={500}
            onChange={(e) => setDescription(e.target.value)}
            aria-label="Description"
          />
        </label>
      </div>

      <div className="flex gap-2">
        <SubmitButton type="submit" disabled={!ready} mutation={save}>
          {entry ? "Save changes" : "Record work entry"}
        </SubmitButton>
        {onCancel && (
          <Button type="button" variant="outline" onClick={onCancel}>
            {entry ? "Cancel edit" : "Cancel"}
          </Button>
        )}
      </div>
    </form>
  );
}
