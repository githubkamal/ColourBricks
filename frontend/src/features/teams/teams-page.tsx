"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { listDepartments } from "@/features/departments/api";
import { ApiError } from "@/lib/api";
import { createTeam, listTeamsGrouped } from "./api";
import { TeamPicker } from "./team-picker";
import type { TeamDto } from "./types";

export function TeamsPage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [departmentId, setDepartmentId] = useState<number | "">("");
  const [selected, setSelected] = useState<TeamDto | null>(null);

  const { data: departments = [] } = useQuery({
    queryKey: ["departments", { includeInactive: false }],
    queryFn: () => listDepartments(false),
  });

  const { data: groups = [], isPending } = useQuery({
    queryKey: ["teams", "grouped"],
    queryFn: () => listTeamsGrouped(),
  });

  const add = useMutation({
    mutationFn: (confirm: boolean) =>
      createTeam({ name: name.trim(), departmentId: Number(departmentId) }, confirm),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.team.name} added`);
        setName("");
        void queryClient.invalidateQueries({ queryKey: ["teams"] });
      } else if (outcome.kind === "needs-confirmation") {
        if (window.confirm(`A similar team name exists. Add "${name.trim()}" anyway?`)) {
          add.mutate(true);
        }
      } else {
        toast.message("That team already exists");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add the team"),
  });

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-lg font-semibold">Teams</h1>

      <form
        className="flex flex-wrap items-end gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (name.trim() && departmentId) add.mutate(false);
        }}
      >
        <label className="space-y-1">
          <span className="text-sm font-medium">Team name</span>
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Electrical Team A"
            aria-label="Team name"
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Department</span>
          <select
            className="block rounded border bg-transparent px-3 py-1.5 text-sm"
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
          </select>
        </label>
        <Button type="submit" disabled={add.isPending || !name.trim() || !departmentId}>
          Add team
        </Button>
      </form>

      <div className="space-y-2">
        <TeamPicker selected={selected} onSelect={setSelected} label="Pick a team" />
        {selected && (
          <p className="text-sm">
            Selected team: <span className="font-medium">{selected.name}</span>
            {selected.departmentName && (
              <span className="text-muted-foreground"> · {selected.departmentName}</span>
            )}
          </p>
        )}
      </div>

      <div className="space-y-4">
        {isPending && <p className="text-muted-foreground text-sm">Loading…</p>}
        {groups.map((group) => (
          <div key={group.departmentId ?? "none"}>
            <h2 className="text-muted-foreground mb-1 text-sm font-semibold uppercase">
              {group.departmentName}
            </h2>
            <ul className="rounded border">
              {group.teams.map((team) => (
                <li key={team.id} className="border-b px-3 py-1.5 text-sm last:border-0">
                  {team.name}
                  {!team.isActive && <span className="text-muted-foreground"> · inactive</span>}
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </div>
  );
}
