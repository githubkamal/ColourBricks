import { Button } from "@/components/ui/button";

/** Shared Prev/Next + "N items · page X of Y" footer for any paginated grid. */
export function PaginationBar({
  page,
  pageCount,
  total,
  onPageChange,
  itemLabel = "items",
}: {
  page: number;
  pageCount: number;
  total: number;
  onPageChange: (page: number) => void;
  itemLabel?: string;
}) {
  if (pageCount <= 1) return null;

  return (
    <div className="flex items-center justify-between text-sm">
      <span className="text-muted-foreground">
        {total} {itemLabel} · page {page} of {pageCount}
      </span>
      <div className="flex gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          Previous
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={page >= pageCount}
          onClick={() => onPageChange(page + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  );
}
