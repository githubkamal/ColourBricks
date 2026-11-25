"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { SubmitButton } from "@/components/ui/submit-button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { Select } from "@/components/ui/select";
import { listDepartments } from "@/features/departments/api";
import { ApiError } from "@/lib/api";
import { createTeam, listTeamsGrouped } from "./api";
import { TeamMasterForm } from "./team-master-form";
import { TeamPicker } from "./team-picker";
import type { TeamDto } from "./types";

/**
 * Teams master (client request, 2026-11-21): pick a department, then a team from
 * that department's list, and maintain the team's full details below. Adding a
 * team is tucked behind "New team" so the screen is about picking one.
 */
export function TeamsPage() {
  const [filterDepartmentId, setFilterDepartmentId] = useState<number | "">("");
  const [selected, setSelected] = useState<TeamDto | null>(null);
  const [adding, setAdding] = useState(false);

  const { data: departments = [] } = useQuery({
    queryKey: ["departments", { includeInactive: false }],
    queryFn: () => listDepartments(false),
  });

  const { data: groups = [], isPending } = useQuery({
    queryKey: ["teams", "grouped"],
    queryFn: () => listTeamsGrouped(),
  });

  const visibleGroups = filterDepartmentId
    ? groups.filter((g) => g.departmentId === filterDepartmentId)
    : groups;

  function changeDepartment(next: number | "") {
    setFilterDepartmentId(next);
    if (selected && next && selected.departmentId !== next) setSelected(null);
  }

  return (
    <div className="max-w-3xl space-y-6">
      <div className="flex items-start justify-between gap-3">
        <PageHeader title="Teams" />
        {!adding && (
          <Button type="button" variant="outline" onClick={() => setAdding(true)}>
            + New team
          </Button>
        )}
      </div>

      {adding && (
        <NewTeamForm
          defaultDepartmentId={filterDepartmentId}
          onCancel={() => setAdding(false)}
          onCreated={(team) => {
            setAdding(false);
            setFilterDepartmentId(team.departmentId ?? "");
            setSelected(team);
          }}
        />
      )}

      <div className="bg-card border-border grid grid-cols-1 gap-3 rounded-xl border p-4 shadow-xs sm:grid-cols-[14rem_1fr]">
        <label className="block space-y-1">
          <span className="text-sm font-medium">Department</span>
          <Select
            className="w-full"
            value={filterDepartmentId}
            aria-label="Filter by department"
            onChange={(e) => changeDepartment(e.target.value ? Number(e.target.value) : "")}
          >
            <option value="">All departments</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
        </label>
        <TeamPicker
          selected={selected}
          onSelect={setSelected}
          label="Team"
          departmentId={filterDepartmentId || null}
        />
      </div>

      {selected ? (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-semibold">
              {selected.name}
              {selected.departmentName && (
                <span className="text-muted-foreground font-normal">
                  {" "}
                  · {selected.departmentName}
                </span>
              )}
            </h2>
            <Button type="button" size="xs" variant="ghost" onClick={() => setSelected(null)}>
              Close
            </Button>
          </div>
          <TeamMasterForm teamId={selected.id} onSaved={setSelected} />
        </div>
      ) : (
        <div className="space-y-4">
          {dataState({
            isPending,
            isEmpty: visibleGroups.length === 0,
            emptyLabel: "No teams in this department yet.",
          })}
          {visibleGroups.map((group) => (
            <div key={group.departmentId ?? "none"}>
              <h2 className="text-muted-foreground mb-1 text-sm font-semibold uppercase">
                {group.departmentName}
              </h2>
              <ul className="bg-card border-border rounded-xl border shadow-xs">
                {group.teams.map((team) => (
                  <li key={team.id} className="border-b last:border-0">
                    <button
                      type="button"
                      className="hover:bg-secondary/60 flex w-full items-center justify-between px-3 py-1.5 text-left text-sm"
                      onClick={() => {
                        setFilterDepartmentId(team.departmentId ?? "");
                        setSelected(team);
                      }}
                    >
                      <span>
                        {team.name}
                        {!team.isActive && (
                          <span className="text-muted-foreground"> · inactive</span>
                        )}
                      </span>
                      <span className="text-muted-foreground text-xs">
                        {team.phone ?? ""}
                        {team.sites && team.sites.length > 0 && (
                          <>
                            {team.phone ? " · " : ""}
                            {team.sites.length} {team.sites.length === 1 ? "site" : "sites"}
                          </>
                        )}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function NewTeamForm({
  defaultDepartmentId,
  onCancel,
  onCreated,
}: {
  defaultDepartmentId: number | "";
  onCancel: () => void;
  onCreated: (team: TeamDto) => void;
}) {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [departmentId, setDepartmentId] = useState<number | "">(defaultDepartmentId);
  const { confirm, dialog } = useConfirmDialog();

  const { data: departments = [] } = useQuery({
    queryKey: ["departments", { includeInactive: false }],
    queryFn: () => listDepartments(false),
  });

  const add = useMutation({
    mutationFn: (confirmed: boolean) =>
      createTeam({ name: name.trim(), departmentId: Number(departmentId) }, confirmed),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.team.name} added — fill in the team details below`);
        void queryClient.invalidateQueries({ queryKey: ["teams"] });
        onCreated(outcome.team);
      } else if (outcome.kind === "needs-confirmation") {
        void confirm({
          title: "Similar team name exists",
          description: `A similar team name exists. Add "${name.trim()}" anyway?`,
          confirmLabel: "Add anyway",
        }).then(({ confirmed }) => {
          if (confirmed) add.mutate(true);
        });
      } else {
        toast.message("That team already exists");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add the team"),
  });

  return (
    <form
      className="bg-card border-border flex flex-wrap items-end gap-2 rounded-xl border p-4 shadow-xs"
      onSubmit={(e) => {
        e.preventDefault();
        if (name.trim() && departmentId) add.mutate(false);
      }}
    >
      {dialog}
      <label className="space-y-1">
        <span className="text-sm font-medium">Team name</span>
        <Input
          value={name}
          autoFocus
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Electrical Team A"
          aria-label="New team name"
        />
      </label>
      <label className="space-y-1">
        <span className="text-sm font-medium">Department</span>
        <Select
          value={departmentId}
          aria-label="Department"
          onChange={(e) => setDepartmentId(e.target.value ? Number(e.target.value) : "")}
        >
          <option value="">Select…</option>
          {departments.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </Select>
      </label>
      <SubmitButton type="submit" disabled={!name.trim() || !departmentId} mutation={add}>
        Add team
      </SubmitButton>
      <Button type="button" variant="ghost" onClick={onCancel}>
        Cancel
      </Button>
    </form>
  );
}
