"use client";

import { useQuery } from "@tanstack/react-query";
import { listItemCategories } from "./api";

export function ItemCategoriesPage() {
  const { data, isPending } = useQuery({
    queryKey: ["item-categories"],
    queryFn: listItemCategories,
  });

  return (
    <div className="max-w-xl space-y-4">
      <h1 className="text-lg font-semibold">Item Categories</h1>
      <div className="rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Name</th>
              <th className="p-2 font-medium">Status</th>
            </tr>
          </thead>
          <tbody>
            {isPending && (
              <tr>
                <td colSpan={2} className="text-muted-foreground p-3 text-center">
                  Loading…
                </td>
              </tr>
            )}
            {!isPending && data?.length === 0 && (
              <tr>
                <td colSpan={2} className="text-muted-foreground p-3 text-center">
                  No categories found.
                </td>
              </tr>
            )}
            {data?.map((category) => (
              <tr key={category.id} className="border-b last:border-0">
                <td className="p-2">{category.name}</td>
                <td className="text-muted-foreground p-2">
                  {category.isActive ? "Active" : "Inactive"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
