"use client";

import { useQuery } from "@tanstack/react-query";
import { listTeamsGrouped } from "./api";
import type { TeamDto } from "./types";

export interface TeamPickerProps {
  selected: TeamDto | null;
  onSelect: (team: TeamDto | null) => void;
  label?: string;
}

/**
 * Team selector grouped by department (BRD §8) — teams are listed under an
 * <optgroup> per department so "Electrical Team A/B/C" sit together.
 */
export function TeamPicker({ selected, onSelect, label = "Team" }: TeamPickerProps) {
  const { data: groups = [], isPending } = useQuery({
    queryKey: ["teams", "grouped"],
    queryFn: () => listTeamsGrouped(),
  });

  const byId = new Map(groups.flatMap((g) => g.teams).map((t) => [t.id, t]));

  return (
    <label className="block space-y-1">
      <span className="text-sm font-medium">{label}</span>
      <select
        className="w-full rounded border bg-background text-foreground px-3 py-1.5 text-sm"
        value={selected?.id ?? ""}
        disabled={isPending}
        aria-label={label}
        onChange={(e) => onSelect(byId.get(Number(e.target.value)) ?? null)}
      >
        <option value="">Select a team…</option>
        {groups.map((group) => (
          <optgroup key={group.departmentId ?? "none"} label={group.departmentName}>
            {group.teams.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </optgroup>
        ))}
      </select>
    </label>
  );
}
