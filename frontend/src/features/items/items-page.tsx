"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { dataState } from "@/components/ui/data-state";
import { PageHeader } from "@/components/ui/page-header";
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
      <PageHeader title="Item Master" />

      <div className="space-y-2">
        <ItemPicker selected={selected} onSelect={setSelected} label="Select or add an item" />
        {selected && (
          <p className="text-sm">
            Selected item: <span className="font-medium">{selected.name}</span>
          </p>
        )}
      </div>

      {dataState({
        isPending,
        isEmpty: !isPending && data?.items.length === 0,
        emptyLabel: "No items yet.",
      }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
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
              {data?.items.map((item) => (
                <tr key={item.id} className="border-b last:border-0">
                  <td className="max-w-xs truncate p-2 break-words">{item.name}</td>
                  <td className="text-muted-foreground p-2">{item.categoryName ?? "—"}</td>
                  <td className="text-muted-foreground p-2">{item.unit}</td>
                  <td className="p-2 tabular-nums">{formatINR(item.defaultRate)}</td>
                  <td className="text-muted-foreground p-2 tabular-nums">{item.taxRate}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
