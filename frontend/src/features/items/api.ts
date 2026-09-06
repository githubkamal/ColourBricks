import { ApiError, apiClient, type PagedResult } from "@/lib/api";
import type {
  CreateItemInput,
  ItemCategoryDto,
  ItemDto,
  ItemNearDuplicate,
  ItemSearchItem,
  UnitDto,
  UpdateItemCategoryInput,
  UpdateItemInput,
} from "./types";

export function searchItems(query: string, limit = 20): Promise<ItemSearchItem[]> {
  const params = new URLSearchParams({ q: query, limit: String(limit) });
  return apiClient.get<ItemSearchItem[]>(`/items/search?${params.toString()}`);
}

export function listItems(
  categoryId?: number,
  search?: string,
  page = 1,
): Promise<PagedResult<ItemSearchItem>> {
  const params = new URLSearchParams({ page: String(page), pageSize: "20" });
  if (categoryId) params.set("categoryId", String(categoryId));
  if (search) params.set("search", search);
  return apiClient.list<ItemSearchItem>(`/items?${params.toString()}`);
}

export function getItem(id: number): Promise<ItemDto> {
  return apiClient.get<ItemDto>(`/items/${id}`);
}

export function listUnits(): Promise<UnitDto[]> {
  return apiClient.get<UnitDto[]>("/items/units");
}

export function listItemCategories(): Promise<ItemCategoryDto[]> {
  return apiClient.get<ItemCategoryDto[]>("/items/categories");
}

export function createItemCategory(name: string): Promise<ItemCategoryDto> {
  return apiClient.post<ItemCategoryDto>("/items/categories", { name });
}

export function updateItemCategory(
  id: number,
  input: UpdateItemCategoryInput,
): Promise<ItemCategoryDto> {
  return apiClient.put<ItemCategoryDto>(`/items/categories/${id}`, input);
}

export function updateItem(id: number, input: UpdateItemInput): Promise<ItemDto> {
  return apiClient.put<ItemDto>(`/items/${id}`, input);
}

export type CreateItemOutcome =
  | { kind: "created"; item: ItemDto }
  | { kind: "needs-confirmation"; nearDuplicates: ItemNearDuplicate[] }
  | { kind: "exact-duplicate"; existingId: number };

export async function createItem(
  input: CreateItemInput,
  confirm: boolean,
): Promise<CreateItemOutcome> {
  try {
    const body = await apiClient.post<
      ItemDto | { requiresConfirmation: true; nearDuplicates: ItemNearDuplicate[] }
    >(`/items?confirm=${confirm}`, input);

    if ("requiresConfirmation" in body) {
      return { kind: "needs-confirmation", nearDuplicates: body.nearDuplicates };
    }
    return { kind: "created", item: body };
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      const existingId = Number(error.problem?.["existingId"] ?? 0);
      if (existingId > 0) return { kind: "exact-duplicate", existingId };
    }
    throw error;
  }
}
