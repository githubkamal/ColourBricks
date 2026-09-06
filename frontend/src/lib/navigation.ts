import {
  Banknote,
  BarChart3,
  Building2,
  HandCoins,
  HardHat,
  Landmark,
  LayoutDashboard,
  Package,
  Receipt,
  Settings,
  Truck,
  UserCheck,
  type LucideIcon,
} from "lucide-react";

/**
 * The navigation tree, transcribed from BRD §68 in order. Each leaf carries the
 * `module.action` permission that gates it; a section is hidden when none of its
 * items are visible (BRD §61 — disabled modules are hidden, not disabled).
 */

export interface NavItem {
  label: string;
  href: string;
  permission: string;
}

export interface NavSection {
  label: string;
  icon: LucideIcon;
  items: NavItem[];
}

export const navigation: NavSection[] = [
  {
    label: "Dashboard",
    icon: LayoutDashboard,
    items: [
      { label: "Company Dashboard", href: "/dashboard/company", permission: "dashboard.view" },
      { label: "Project Dashboard", href: "/dashboard/project", permission: "dashboard.view" },
      { label: "Notifications", href: "/notifications", permission: "dashboard.view" },
    ],
  },
  {
    label: "Projects",
    icon: Building2,
    items: [
      { label: "Ongoing Projects", href: "/projects?status=ongoing", permission: "projects.view" },
      {
        label: "Completed Projects",
        href: "/projects?status=completed",
        permission: "projects.view",
      },
      { label: "Project Master", href: "/projects", permission: "projects.view" },
      {
        label: "Project Ledger",
        href: "/projects?intent=ledger",
        permission: "projects.view",
      },
      { label: "Project Income", href: "/project-income", permission: "project_income.view" },
      { label: "Project Expenses", href: "/project-expenses", permission: "project_expenses.view" },
      { label: "Project Budget", href: "/projects?intent=budget", permission: "budget.view" },
      {
        label: "Project Profit/Loss",
        href: "/projects?intent=pnl",
        permission: "profit_loss.view",
      },
    ],
  },
  {
    label: "Vendors",
    icon: Truck,
    items: [
      { label: "Vendor Master", href: "/vendors", permission: "vendors.view" },
      {
        label: "Purchase Orders",
        href: "/vendors/purchase-orders",
        permission: "materials.view",
      },
      { label: "Payments", href: "/vendors/payments", permission: "payments.view" },
      {
        label: "Outstanding",
        href: "/reports/explorer?report=vendor-outstanding",
        permission: "reports.view",
      },
      { label: "Statements", href: "/vendors/statements", permission: "vendors.view" },
      {
        label: "Payment Allocation",
        href: "/vendors/allocation",
        permission: "vendor_payment_allocation.view",
      },
      {
        label: "Allocation Report",
        href: "/vendors/allocation-report",
        permission: "vendor_payment_allocation.view",
      },
    ],
  },
  {
    label: "Field Officers",
    icon: UserCheck,
    items: [
      { label: "Field Officer Master", href: "/field-officers", permission: "vendors.view" },
      { label: "Payments", href: "/field-officers/payments", permission: "payments.view" },
      {
        label: "Outstanding",
        href: "/reports/explorer?report=field-officer-outstanding",
        permission: "reports.view",
      },
      { label: "No-project Bills", href: "/field-officers/bills", permission: "vendors.view" },
      { label: "Statement", href: "/field-officers/statements", permission: "vendors.view" },
    ],
  },
  {
    label: "Labour / Subcontractors",
    icon: HardHat,
    items: [
      { label: "Departments", href: "/labour/departments", permission: "labour.view" },
      { label: "Teams", href: "/labour/teams", permission: "labour.view" },
      { label: "Work Entries", href: "/labour/work", permission: "labour.view" },
      { label: "Payments", href: "/labour/work", permission: "labour.view" },
      { label: "Outstanding", href: "/labour/statements", permission: "labour.view" },
      { label: "Statements", href: "/labour/statements", permission: "labour.view" },
    ],
  },
  {
    label: "Materials",
    icon: Package,
    items: [
      { label: "Item Master", href: "/materials", permission: "materials.view" },
      { label: "Item Categories", href: "/materials/categories", permission: "materials.view" },
      { label: "Purchases", href: "/materials/purchases", permission: "materials.view" },
    ],
  },
  {
    label: "Temple Donations",
    icon: Landmark,
    items: [
      { label: "Temple Master", href: "/donations/temples", permission: "temple_donations.view" },
      { label: "Project Donations", href: "/donations", permission: "temple_donations.view" },
      {
        label: "Donation Payments",
        href: "/donations",
        permission: "temple_donations.view",
      },
      {
        label: "Reports",
        href: "/reports/explorer?report=donation-project-wise",
        permission: "reports.view",
      },
    ],
  },
  {
    label: "Loans",
    icon: HandCoins,
    items: [
      { label: "Loan Master", href: "/loans", permission: "loans.view" },
      { label: "EMI Schedule", href: "/loans/schedule", permission: "emi.view" },
      { label: "EMI Payments", href: "/loans/payments", permission: "emi.view" },
      { label: "Outstanding", href: "/loans/outstanding", permission: "loans.view" },
    ],
  },
  {
    label: "Other Expenses",
    icon: Receipt,
    items: [
      {
        label: "Personal Expenses",
        href: "/expenses/personal",
        permission: "personal_expenses.view",
      },
      { label: "Office Expenses", href: "/expenses/office", permission: "office_expenses.view" },
      { label: "Savings", href: "/expenses/savings", permission: "savings.view" },
      {
        label: "Common Expense Allocation",
        href: "/expenses/common",
        permission: "common_expenses.view",
      },
      {
        label: "Customized Expenses",
        href: "/expenses/customized",
        permission: "customized_work.view",
      },
    ],
  },
  {
    label: "Accounts",
    icon: Banknote,
    items: [
      { label: "Cash Accounts", href: "/accounts/cash", permission: "accounts.view" },
      { label: "Bank Accounts", href: "/accounts/bank", permission: "accounts.view" },
      { label: "Payment Modes", href: "/accounts/payment-modes", permission: "accounts.view" },
      {
        label: "Transactions",
        href: "/reports/explorer?report=bank-wise-report",
        permission: "reports.view",
      },
      {
        label: "Bank Statement Upload",
        href: "/reconciliation/upload",
        permission: "bank_reconciliation.view",
      },
      {
        label: "Reconciliation Queue",
        href: "/reconciliation",
        permission: "bank_reconciliation.view",
      },
      {
        label: "Reconciled Transactions",
        href: "/reconciliation?status=Reconciled",
        permission: "bank_reconciliation.view",
      },
      {
        label: "Excluded Transactions",
        href: "/reconciliation?status=Excluded",
        permission: "bank_reconciliation.view",
      },
    ],
  },
  {
    label: "Reports",
    icon: BarChart3,
    items: [
      { label: "Report Explorer", href: "/reports/explorer", permission: "reports.view" },
      {
        label: "Project Reports",
        href: "/reports/explorer?report=project-financial-summary",
        permission: "reports.view",
      },
      {
        label: "Vendor Reports",
        href: "/reports/explorer?report=vendor-purchase",
        permission: "reports.view",
      },
      { label: "Loan Reports", href: "/reports/loans", permission: "reports.view" },
      {
        label: "Subcontractor Reports",
        href: "/reports/explorer?report=subcontractor-work-value-vs-payment",
        permission: "reports.view",
      },
      {
        label: "Donation Reports",
        href: "/reports/explorer?report=donation-project-wise",
        permission: "reports.view",
      },
      {
        label: "Cash/Bank Reports",
        href: "/reports/explorer?report=account-statement",
        permission: "reports.view",
      },
      {
        label: "Bank Reconciliation Reports",
        href: "/reports/explorer?report=bank-reconciliation-report",
        permission: "reports.view",
      },
      {
        label: "Profit/Loss",
        href: "/reports/explorer?report=company-pnl",
        permission: "reports.view",
      },
      {
        label: "Outstanding",
        href: "/reports/explorer?report=company-outstanding",
        permission: "reports.view",
      },
      {
        label: "Budget vs Actual",
        href: "/reports/explorer?report=project-budget-vs-actual",
        permission: "reports.view",
      },
      {
        label: "Common Expense Allocation",
        href: "/reports/common-expenses",
        permission: "reports.view",
      },
    ],
  },
  {
    label: "Administration",
    icon: Settings,
    items: [
      { label: "Users", href: "/admin/users", permission: "users.view" },
      { label: "Roles", href: "/admin/roles", permission: "roles.view" },
      { label: "Permissions", href: "/admin/roles", permission: "roles.view" },
      {
        label: "Module Configuration",
        href: "/admin/roles",
        permission: "roles.view",
      },
      { label: "Masters", href: "/admin/masters", permission: "admin_configuration.view" },
      { label: "Audit Logs", href: "/admin/audit-logs", permission: "audit_trail.view" },
      {
        label: "Integrity Check",
        href: "/admin/integrity-check",
        permission: "admin_configuration.view",
      },
      { label: "System Settings", href: "/admin/settings", permission: "admin_configuration.view" },
      {
        label: "Notification Channels",
        href: "/admin/notification-channels",
        permission: "admin_configuration.edit",
      },
    ],
  },
];

/** Whether a granted permission set includes the given permission — "*" (an
 * Administrator's role) always does, same rule everywhere a permission gates
 * something in the UI. */
export function hasPermission(granted: readonly string[], permission: string): boolean {
  return granted.includes("*") || granted.includes(permission);
}

/** The sections and items the given permission set can see. */
export function visibleNavigation(granted: readonly string[]): NavSection[] {
  return navigation
    .map((section) => ({
      ...section,
      items: section.items.filter((i) => hasPermission(granted, i.permission)),
    }))
    .filter((section) => section.items.length > 0);
}
