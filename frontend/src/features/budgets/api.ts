import { apiClient } from "@/lib/api";

export interface BudgetLine {
  categoryId: number;
  categoryName: string;
  bucket: string;
  amount: number;
}

export interface ProjectBudget {
  projectId: number;
  revisionNumber: number;
  approachingThresholdPercent: number;
  note: string | null;
  revisedAtUtc: string;
  revisedByUserId: number | null;
  estimatedCost: number;
  budgetTotal: number;
  varianceFromEstimate: number;
  lines: BudgetLine[];
}

export interface BudgetRevisionSummary {
  revisionNumber: number;
  budgetTotal: number;
  note: string | null;
  revisedAtUtc: string;
  revisedByUserId: number | null;
}

export interface SaveBudgetInput {
  lines: { categoryId: number; amount: number }[];
  approachingThresholdPercent: number;
  note?: string | null;
}

export function getProjectBudget(projectId: number): Promise<ProjectBudget | null> {
  return apiClient.get<ProjectBudget | null>(`/projects/${projectId}/budget`);
}

export function listBudgetRevisions(projectId: number): Promise<BudgetRevisionSummary[]> {
  return apiClient.get<BudgetRevisionSummary[]>(`/projects/${projectId}/budget/revisions`);
}

export function saveProjectBudget(
  projectId: number,
  input: SaveBudgetInput,
): Promise<ProjectBudget> {
  return apiClient.post<ProjectBudget>(`/projects/${projectId}/budget`, input);
}
