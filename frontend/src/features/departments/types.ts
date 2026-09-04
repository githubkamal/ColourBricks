export interface DepartmentDto {
  id: number;
  name: string;
  isActive: boolean;
  concurrencyStamp: string;
}

export interface CreateDepartmentInput {
  name: string;
}
