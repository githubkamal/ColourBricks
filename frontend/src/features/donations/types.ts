export type DonationBasis = "Percentage" | "Fixed";

export interface DonationTempleSplit {
  templeId: number;
  templeName: string;
  amount: number;
}

export interface ProjectDonation {
  id: number;
  projectId: number;
  basis: DonationBasis;
  percentage: number | null;
  fixedAmount: number | null;
  contractValueSnapshot: number;
  donationAmount: number;
  paidAmount: number;
  excessPaid: number;
  needsReview: boolean;
  temples: DonationTempleSplit[];
  concurrencyStamp: string;
}

export interface UpsertDonationInput {
  basis: DonationBasis;
  percentage: number | null;
  fixedAmount: number | null;
  temples: { templeId: number; amount: number }[];
}
