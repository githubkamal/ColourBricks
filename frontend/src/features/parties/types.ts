export const PARTY_TYPES = [
  "Vendor",
  "Subcontractor",
  "Client",
  "Temple",
  "Lender",
  "FieldOfficer",
] as const;
export type PartyType = (typeof PARTY_TYPES)[number];

export interface PartySearchItem {
  id: number;
  name: string;
  types: PartyType[];
  category: string | null;
  isActive: boolean;
}

export interface PartyDto extends PartySearchItem {
  contactPerson: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  gstNumber: string | null;
  bankDetails: string | null;
  paymentTerms: string | null;
  departmentId: number | null;
  isActive: boolean;
  concurrencyStamp: string;
}

export interface NearDuplicate {
  id: number;
  name: string;
  types: PartyType[];
}

export interface CreatePartyInput {
  name: string;
  types: PartyType[];
  category?: string | null;
  contactPerson?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  gstNumber?: string | null;
  bankDetails?: string | null;
  paymentTerms?: string | null;
}

export interface UpdatePartyInput {
  name: string;
  types: PartyType[];
  isActive: boolean;
  concurrencyStamp: string;
  category?: string | null;
  contactPerson?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  gstNumber?: string | null;
  bankDetails?: string | null;
  paymentTerms?: string | null;
  departmentId?: number | null;
}
