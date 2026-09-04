import { notFound } from "next/navigation";
import { AccountDetail } from "@/features/accounts/account-detail";

export default async function AccountDetailPage({ params }: PageProps<"/accounts/[id]">) {
  const { id } = await params;
  const accountId = Number(id);
  if (!Number.isInteger(accountId) || accountId < 1) notFound();

  return <AccountDetail id={accountId} />;
}
