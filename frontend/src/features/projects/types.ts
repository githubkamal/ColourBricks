export const PROJECT_STATUSES = ["Ongoing", "Completed", "OnHold", "Cancelled"] as const;
export type ProjectStatus = (typeof PROJECT_STATUSES)[number];

export const PROJECT_STATUS_LABELS: Record<ProjectStatus, string> = {
  Ongoing: "Ongoing",
  Completed: "Completed",
  OnHold: "On hold",
  Cancelled: "Cancelled",
};

export interface ProjectListItem {
  id: number;
  code: string;
  name: string;
  status: ProjectStatus;
  startDate: string;
  expectedEndDate: string | null;
  contractValue: number;
  estimatedCost: number;
  managerId: number | null;
}

export interface ProjectDetail extends ProjectListItem {
  clientId: number | null;
  siteAddress: string | null;
  contactDetails: string | null;
  actualEndDate: string | null;
  expectedProfit: number | null;
  notes: string | null;
  isActive: boolean;
  concurrencyStamp: string;
}

export interface CreateProjectInput {
  name: string;
  code?: string | null;
  startDate: string;
  expectedEndDate?: string | null;
  contractValue: number;
  estimatedCost: number;
  status?: ProjectStatus;
  siteAddress?: string | null;
  contactDetails?: string | null;
  notes?: string | null;
}
