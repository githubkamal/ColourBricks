export interface UserListItem {
  id: number;
  name: string;
  email: string;
  mobile: string | null;
  roleId: number | null;
  roleName: string | null;
  departmentId: number | null;
  isActive: boolean;
  isAdministrator: boolean;
  assignedProjectIds: number[];
  concurrencyStamp: string;
}

export interface RoleOption {
  id: number;
  name: string;
}

export interface CreateUserInput {
  name: string;
  email: string;
  password: string;
  mobile?: string | null;
  roleId?: number | null;
}

export interface UpdateUserInput {
  name: string;
  email: string;
  isActive: boolean;
  concurrencyStamp: string;
  mobile?: string | null;
  roleId?: number | null;
}
