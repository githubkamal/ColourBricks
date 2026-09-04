import { notFound } from "next/navigation";
import { PurchaseOrderDetailPage } from "@/features/purchase-orders/purchase-order-detail-page";

export default async function Page({ params }: PageProps<"/vendors/purchase-orders/[id]">) {
  const { id } = await params;
  const purchaseOrderId = Number(id);
  if (!Number.isInteger(purchaseOrderId) || purchaseOrderId < 1) notFound();

  return <PurchaseOrderDetailPage id={purchaseOrderId} />;
}
