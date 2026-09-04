import { apiClient } from "@/lib/api";

export interface Loan {
  id: number;
  projectId: number | null;
  lenderId: number;
  lenderName: string;
  principalAmount: number;
  annualInterestRatePercent: number;
  startDate: string;
  tenureMonths: number;
  emiAmount: number | null;
  emiStartDate: string;
  emiEndDate: string | null;
  disbursementAccountId: number;
  disbursementDate: string;
  reference: string | null;
  notes: string | null;
  status: string;
  outstandingPrincipal: number;
}

export interface RecordLoanInput {
  lenderId: number;
  principalAmount: number;
  annualInterestRatePercent: number;
  startDate: string;
  tenureMonths: number;
  emiStartDate: string;
  disbursementAccountId: number;
  disbursementDate: string;
  projectId?: number | null;
  emiAmount?: number | null;
  reference?: string | null;
  notes?: string | null;
}

export function recordLoan(input: RecordLoanInput): Promise<Loan> {
  return apiClient.post("/loans", input);
}

export function listLoans(projectId?: number, lenderId?: number): Promise<Loan[]> {
  const params = new URLSearchParams();
  if (projectId) params.set("projectId", String(projectId));
  if (lenderId) params.set("lenderId", String(lenderId));
  const q = params.toString();
  return apiClient.get<Loan[]>(`/loans${q ? `?${q}` : ""}`);
}

export function getLoan(id: number): Promise<Loan> {
  return apiClient.get(`/loans/${id}`);
}

export function reverseLoan(id: number, reason: string): Promise<void> {
  return apiClient.post(`/loans/${id}/reverse`, { reason });
}

export interface LoanEmiInstalment {
  id: number;
  loanId: number;
  instalmentNo: number;
  dueDate: string;
  openingPrincipal: number;
  emiAmount: number;
  principalComponent: number;
  interestComponent: number;
  closingPrincipal: number;
  status: string;
  paidAmount: number;
  paidDate: string | null;
  overdue: boolean;
}

export function getSchedule(loanId: number): Promise<LoanEmiInstalment[]> {
  return apiClient.get(`/loans/${loanId}/schedule`);
}

export function generateSchedule(
  loanId: number,
  emiAmount?: number | null,
): Promise<LoanEmiInstalment[]> {
  return apiClient.post(`/loans/${loanId}/schedule`, { emiAmount: emiAmount ?? null });
}

export function regenerateSchedule(
  loanId: number,
  newAnnualRatePercent: number,
  emiAmount?: number | null,
): Promise<LoanEmiInstalment[]> {
  return apiClient.post(`/loans/${loanId}/schedule/regenerate`, {
    newAnnualRatePercent,
    emiAmount: emiAmount ?? null,
  });
}

export interface LoanEmiPayment {
  id: number;
  loanId: number;
  instalmentId: number | null;
  date: string;
  amount: number;
  principalPaid: number;
  interestPaid: number;
  paymentModeId: number;
  accountId: number | null;
  referenceNo: string | null;
  isPrepayment: boolean;
  status: string;
}

export interface PayEmiInput {
  instalmentId: number;
  date: string;
  paymentModeId: number;
  amount?: number | null;
  accountId?: number | null;
  referenceNo?: string | null;
}

export function payEmi(loanId: number, input: PayEmiInput): Promise<LoanEmiPayment> {
  return apiClient.post(`/loans/${loanId}/emi-payments`, input);
}

export interface PrepayLoanInput {
  amount: number;
  date: string;
  paymentModeId: number;
  accountId?: number | null;
  referenceNo?: string | null;
}

export function prepayLoan(loanId: number, input: PrepayLoanInput): Promise<LoanEmiPayment> {
  return apiClient.post(`/loans/${loanId}/prepayments`, input);
}

export function listEmiPayments(loanId: number): Promise<LoanEmiPayment[]> {
  return apiClient.get(`/loans/${loanId}/emi-payments`);
}

export function reverseEmiPayment(paymentId: number, reason: string): Promise<void> {
  return apiClient.post(`/emi-payments/${paymentId}/reverse`, { reason });
}

export interface LoanOutstandingRow {
  projectId: number | null;
  projectName: string;
  loanCount: number;
  principalOutstanding: number;
}

export interface LoanOutstandingSummary {
  companyPrincipalOutstanding: number;
  byProject: LoanOutstandingRow[];
}

export function loanOutstandingSummary(): Promise<LoanOutstandingSummary> {
  return apiClient.get("/loans/outstanding-summary");
}

// ── Loan reports (BRD §54) — a dedicated controller, not the generic report
// framework, so these are wired up separately from the rest of Report Explorer. ──

export interface ProjectWiseLoanRow {
  projectId: number | null;
  projectName: string;
  loanCount: number;
  loanAmount: number;
  principalPaid: number;
  outstandingPrincipal: number;
  interestPaid: number;
}

export interface LoanOutstandingReportRow {
  loanId: number;
  lenderName: string;
  projectId: number | null;
  projectName: string;
  loanAmount: number;
  principalPaid: number;
  interestPaid: number;
  outstandingPrincipal: number;
  nextDueDate: string | null;
  status: string;
}

export interface EmiPaidRow {
  paymentId: number;
  loanId: number;
  lenderName: string;
  date: string;
  amount: number;
  principalPaid: number;
  interestPaid: number;
  isPrepayment: boolean;
  status: string;
}

export interface EmiPendingRow {
  loanId: number;
  lenderName: string;
  instalmentNo: number;
  dueDate: string;
  emiAmount: number;
  amountDue: number;
  daysOverdue: number;
}

export interface PrincipalVsInterestRow {
  loanId: number;
  lenderName: string;
  loanAmount: number;
  principalPaid: number;
  interestPaid: number;
  outstandingPrincipal: number;
}

export interface DateWiseEmiRow {
  date: string;
  paymentCount: number;
  amount: number;
  principalPaid: number;
  interestPaid: number;
}

export function loanReportProjectWise(): Promise<ProjectWiseLoanRow[]> {
  return apiClient.get("/reports/loans/project-wise");
}

export function loanReportOutstanding(projectId?: number): Promise<LoanOutstandingReportRow[]> {
  const q = projectId ? `?projectId=${projectId}` : "";
  return apiClient.get(`/reports/loans/outstanding${q}`);
}

export function loanReportSchedule(loanId: number): Promise<LoanEmiInstalment[]> {
  return apiClient.get(`/reports/loans/schedule/${loanId}`);
}

export function loanReportEmiPaid(
  loanId?: number,
  dateFrom?: string,
  dateTo?: string,
): Promise<EmiPaidRow[]> {
  const params = new URLSearchParams();
  if (loanId) params.set("loanId", String(loanId));
  if (dateFrom) params.set("dateFrom", dateFrom);
  if (dateTo) params.set("dateTo", dateTo);
  const q = params.toString();
  return apiClient.get(`/reports/loans/emi-paid${q ? `?${q}` : ""}`);
}

export function loanReportEmiPending(loanId?: number): Promise<EmiPendingRow[]> {
  const q = loanId ? `?loanId=${loanId}` : "";
  return apiClient.get(`/reports/loans/emi-pending${q}`);
}

export function loanReportPrincipalVsInterest(loanId?: number): Promise<PrincipalVsInterestRow[]> {
  const q = loanId ? `?loanId=${loanId}` : "";
  return apiClient.get(`/reports/loans/principal-vs-interest${q}`);
}

export function loanReportDateWise(dateFrom?: string, dateTo?: string): Promise<DateWiseEmiRow[]> {
  const params = new URLSearchParams();
  if (dateFrom) params.set("dateFrom", dateFrom);
  if (dateTo) params.set("dateTo", dateTo);
  const q = params.toString();
  return apiClient.get(`/reports/loans/date-wise${q ? `?${q}` : ""}`);
}
