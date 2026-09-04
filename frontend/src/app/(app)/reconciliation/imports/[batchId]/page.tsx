import { notFound } from "next/navigation";
import { BankImportReviewPage } from "@/features/bank-import/bank-import-review-page";

export default async function BankImportReviewRoute({
  params,
}: PageProps<"/reconciliation/imports/[batchId]">) {
  const { batchId } = await params;
  const id = Number(batchId);
  if (!Number.isInteger(id) || id < 1) notFound();

  return <BankImportReviewPage batchId={id} />;
}
