export interface ItemSearchItem {
  id: number;
  name: string;
  categoryName: string | null;
  unit: string;
  defaultRate: number;
  taxRate: number;
}

export interface ItemDto extends ItemSearchItem {
  categoryId: number | null;
  isActive: boolean;
  concurrencyStamp: string;
}

export interface ItemNearDuplicate {
  id: number;
  name: string;
  categoryName: string | null;
}

export interface CreateItemInput {
  name: string;
  unit: string;
  defaultRate: number;
  taxRate: number;
  categoryId?: number | null;
}

export interface UnitDto {
  id: number;
  code: string;
  sortOrder: number;
}

export interface ItemCategoryDto {
  id: number;
  name: string;
  isActive: boolean;
  concurrencyStamp: string;
}
