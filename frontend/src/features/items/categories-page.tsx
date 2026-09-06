"use client";

import { useQuery } from "@tanstack/react-query";
import { dataState } from "@/components/ui/data-state";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { listItemCategories } from "./api";

export function ItemCategoriesPage() {
  const { data, isPending } = useQuery({
    queryKey: ["item-categories"],
    queryFn: listItemCategories,
  });

  return (
    <div className="max-w-xl space-y-4">
      <PageHeader title="Item Categories" />
      {dataState({
        isPending,
        isEmpty: !isPending && data?.length === 0,
        emptyLabel: "No categories found.",
      }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Name</th>
                <th className="p-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody>
              {data?.map((category) => (
                <tr key={category.id} className="border-b last:border-0">
                  <td className="p-2">{category.name}</td>
                  <td className="p-2">
                    <StatusBadge status={category.isActive ? "Active" : "Inactive"} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
