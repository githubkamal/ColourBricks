export interface PaymentModeDto {
  id: number;
  name: string;
  requiresAccount: boolean;
  requiresReference: boolean;
  sortOrder: number;
  isActive: boolean;
  concurrencyStamp: string;
}

export interface CreatePaymentModeInput {
  name: string;
  requiresAccount?: boolean;
  requiresReference?: boolean;
}
