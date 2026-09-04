"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { formatINR } from "@/lib/format";
import { listItems } from "./api";
import { ItemPicker } from "./item-picker";
import type { ItemSearchItem } from "./types";

export function ItemsPage() {
  const [selected, setSelected] = useState<ItemSearchItem | null>(null);

  const { data, isPending } = useQuery({
    queryKey: ["items", { page: 1 }],
    queryFn: () => listItems(),
  });

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-lg font-semibold">Item Master</h1>

      <div className="space-y-2">
        <ItemPicker selected={selected} onSelect={setSelected} label="Select or add an item" />
        {selected && (
          <p className="text-sm">
            Selected item: <span className="font-medium">{selected.name}</span>
          </p>
        )}
      </div>

      <div className="rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Name</th>
              <th className="p-2 font-medium">Category</th>
              <th className="p-2 font-medium">Unit</th>
              <th className="p-2 font-medium">Default Rate</th>
              <th className="p-2 font-medium">Tax %</th>
            </tr>
          </thead>
          <tbody>
            {isPending && (
              <tr>
                <td colSpan={5} className="text-muted-foreground p-3 text-center">
                  Loading…
                </td>
              </tr>
            )}
            {data?.items.length === 0 && (
              <tr>
                <td colSpan={5} className="text-muted-foreground p-3 text-center">
                  No items yet.
                </td>
              </tr>
            )}
            {data?.items.map((item) => (
              <tr key={item.id} className="border-b last:border-0">
                <td className="p-2">{item.name}</td>
                <td className="text-muted-foreground p-2">{item.categoryName ?? "—"}</td>
                <td className="text-muted-foreground p-2">{item.unit}</td>
                <td className="p-2">{formatINR(item.defaultRate)}</td>
                <td className="text-muted-foreground p-2">{item.taxRate}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
