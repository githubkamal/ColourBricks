import { apiClient, type PagedResult } from "@/lib/api";
import type {
  CreateProjectInput,
  ProjectDetail,
  ProjectListItem,
  ProjectStatus,
  UpdateProjectInput,
} from "./types";

export interface ProjectListParams {
  status?: ProjectStatus;
  search?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: "asc" | "desc";
}

export function listProjects(params: ProjectListParams): Promise<PagedResult<ProjectListItem>> {
  const query = new URLSearchParams();
  if (params.status) query.set("status", params.status);
  if (params.search) query.set("search", params.search);
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  if (params.sortBy) query.set("sortBy", params.sortBy);
  if (params.sortDir) query.set("sortDir", params.sortDir);
  return apiClient.list<ProjectListItem>(`/projects?${query.toString()}`);
}

export function getProject(id: number): Promise<ProjectDetail> {
  return apiClient.get<ProjectDetail>(`/projects/${id}`);
}

export function createProject(input: CreateProjectInput): Promise<ProjectDetail> {
  return apiClient.post<ProjectDetail>("/projects", input);
}

export function updateProject(id: number, input: UpdateProjectInput): Promise<ProjectDetail> {
  return apiClient.put<ProjectDetail>(`/projects/${id}`, input);
}
