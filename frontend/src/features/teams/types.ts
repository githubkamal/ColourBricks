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
