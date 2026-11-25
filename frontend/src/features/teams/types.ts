export interface TeamSiteDto {
  id: number;
  projectId: number | null;
  projectName: string | null;
  siteLocation: string | null;
  contractValue: number;
}

export interface TeamDto {
  id: number;
  name: string;
  departmentId: number | null;
  departmentName: string | null;
  contactPerson: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  paymentTerms: string | null;
  bankDetails: string | null;
  isActive: boolean;
  concurrencyStamp: string;
  alternatePhone?: string | null;
  workingArea?: string | null;
  bankAccountHolderName?: string | null;
  bankName?: string | null;
  bankAccountNumber?: string | null;
  bankIfsc?: string | null;
  sites?: TeamSiteDto[];
}

export interface TeamGroup {
  departmentId: number | null;
  departmentName: string;
  teams: TeamDto[];
}

export interface CreateTeamInput {
  name: string;
  departmentId: number;
}

export interface TeamSiteInput {
  projectId: number | null;
  siteLocation: string | null;
  contractValue: number;
}

export interface UpdateTeamInput {
  name: string;
  departmentId: number;
  isActive: boolean;
  concurrencyStamp: string;
  contactPerson: string | null;
  phone: string | null;
  alternatePhone: string | null;
  email: string | null;
  address: string | null;
  workingArea: string | null;
  paymentTerms: string | null;
  bankDetails: string | null;
  bankAccountHolderName: string | null;
  bankName: string | null;
  bankAccountNumber: string | null;
  bankIfsc: string | null;
  sites: TeamSiteInput[];
}
