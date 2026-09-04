export const ACCOUNT_TYPES = ["Cash", "Bank"] as const;
export type AccountType = (typeof ACCOUNT_TYPES)[number];

export interface AccountListItem {
  id: number;
  name: string;
  type: AccountType;
  bankName: string | null;
  accountNumber: string | null;
  isActive: boolean;
}

export interface AccountDetail {
  id: number;
  name: string;
  type: AccountType;
  bankName: string | null;
  accountNumber: string | null;
  ifsc: string | null;
  openingBalance: number;
  openingBalanceDate: string;
  balance: number;
  openingBalanceLocked: boolean;
  isActive: boolean;
  concurrencyStamp: string;
}

export interface CreateAccountInput {
  name: string;
  type: AccountType;
  openingBalance: number;
  openingBalanceDate: string;
  bankName?: string | null;
  accountNumber?: string | null;
  ifsc?: string | null;
}
