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

export async function getProjectBudget(projectId: number): Promise<ProjectBudget | null> {
  // No budget revision yet is a normal, empty state (not an error) — the backend
  // may respond 200 with a JSON "null" body or 204 with none at all, and the
  // shared apiClient maps the latter to `undefined`, which React Query rejects
  // as a query result. Normalise both to `null` here.
  const result = await apiClient.get<ProjectBudget | null>(`/projects/${projectId}/budget`);
  return result ?? null;
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
