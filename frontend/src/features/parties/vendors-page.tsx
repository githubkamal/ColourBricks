"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { listParties } from "./api";
import { PartyPicker } from "./party-picker";
import type { PartySearchItem, PartyType } from "./types";

export function VendorsPage({
  partyType = "Vendor",
  title = "Vendors",
  pickerLabel = "Select or add a vendor",
}: {
  partyType?: PartyType;
  title?: string;
  pickerLabel?: string;
}) {
  const [selected, setSelected] = useState<PartySearchItem | null>(null);

  const { data, isPending } = useQuery({
    queryKey: ["parties", { type: partyType }],
    queryFn: () => listParties(partyType),
  });

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-lg font-semibold">{title}</h1>

      <div className="space-y-2">
        <PartyPicker
          type={partyType}
          label={pickerLabel}
          selected={selected}
          onSelect={setSelected}
        />
        {selected && (
          <p className="text-sm">
            Selected vendor: <span className="font-medium">{selected.name}</span>
          </p>
        )}
      </div>

      <div className="rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Name</th>
              <th className="p-2 font-medium">Roles</th>
              <th className="p-2 font-medium">Category</th>
            </tr>
          </thead>
          <tbody>
            {isPending && (
              <tr>
                <td colSpan={3} className="text-muted-foreground p-3 text-center">
                  Loading…
                </td>
              </tr>
            )}
            {data?.items.length === 0 && (
              <tr>
                <td colSpan={3} className="text-muted-foreground p-3 text-center">
                  No vendors yet.
                </td>
              </tr>
            )}
            {data?.items.map((vendor) => (
              <tr key={vendor.id} className="border-b last:border-0">
                <td className="p-2">{vendor.name}</td>
                <td className="text-muted-foreground p-2">{vendor.types.join(", ")}</td>
                <td className="text-muted-foreground p-2">{vendor.category ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
