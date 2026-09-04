export const INCOME_TYPES = [
  "ClientAdvance",
  "Stage",
  "Milestone",
  "Additional",
  "Final",
  "Other",
] as const;
export type IncomeType = (typeof INCOME_TYPES)[number];

export const INCOME_TYPE_LABELS: Record<IncomeType, string> = {
  ClientAdvance: "Client Advance",
  Stage: "Stage Payment",
  Milestone: "Milestone Payment",
  Additional: "Additional Payment",
  Final: "Final Payment",
  Other: "Other Project Income",
};

export interface Receipt {
  id: number;
  projectId: number;
  type: IncomeType;
  date: string;
  amount: number;
  paymentModeId: number;
  paymentModeName: string;
  accountId: number | null;
  accountName: string | null;
  referenceNo: string | null;
  description: string | null;
  status: "Active" | "Reversed";
  concurrencyStamp: string;
}

export interface RecordReceiptInput {
  projectId: number;
  type: IncomeType;
  date: string;
  amount: number;
  paymentModeId: number;
  accountId?: number | null;
  referenceNo?: string | null;
  description?: string | null;
}
