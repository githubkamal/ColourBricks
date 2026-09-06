using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using ColourBricks.Infrastructure.Auditing;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ColourBricks.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer. The API project calls this from
/// <c>Program.cs</c>; nothing else wires up EF Core (plan.md §4).
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString =
            configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not found.");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddScoped<AppendOnlyGuardInterceptor>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<Application.Settings.ISystemSettingsService, Settings.SystemSettingsService>();

        // Authentication (plan.md §9, P0-T04).
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName);
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProjectScopeFilter, ProjectScopeFilter>();
        services.AddScoped<IdentitySeeder>();

        // Masters (Phase 1).
        services.AddScoped<Application.Projects.IProjectService, Projects.ProjectService>();
        services.AddScoped<Application.Parties.IPartyService, Parties.PartyService>();
        services.AddScoped<Application.Items.IItemService, Items.ItemService>();
        services.AddScoped<Application.Departments.IDepartmentService, Departments.DepartmentService>();
        services.AddScoped<Application.Teams.ITeamService, Teams.TeamService>();
        services.AddScoped<Application.Payments.IPaymentModeService, Payments.PaymentModeService>();
        services.AddScoped<Application.Accounts.IAccountService, Accounts.AccountService>();
        services.AddScoped<Application.Temples.ITempleService, Temples.TempleService>();
        services.AddScoped<Application.Donations.IProjectDonationService, Donations.ProjectDonationService>();
        services.AddScoped<Application.Users.IUserAdminService, Identity.UserAdminService>();
        services.AddScoped<Application.Roles.IRoleAdminService, Identity.RoleAdminService>();

        // Core transactions (Phase 2).
        services.AddScoped<Application.Ledger.ILedgerPostingService, Ledger.LedgerPostingService>();
        services.AddScoped<Application.Ledger.ILedgerQueryService, Ledger.LedgerQueryService>();
        services.AddScoped<Application.Ledger.IExpenseCategoryService, Ledger.ExpenseCategoryService>();
        services.AddScoped<Application.Receipts.IReceiptService, Receipts.ReceiptService>();
        services.AddScoped<Application.VendorPurchases.IVendorPurchaseService, VendorPurchases.VendorPurchaseService>();
        services.AddScoped<Application.DirectExpenses.IDirectExpenseService, DirectExpenses.DirectExpenseService>();
        services.AddScoped<Application.Labour.ILabourService, Labour.LabourService>();
        services.AddScoped<Application.Donations.IDonationPaymentService, Donations.DonationPaymentService>();
        services.AddScoped<Application.CustomWork.ICustomWorkService, CustomWork.CustomWorkService>();
        services.AddScoped<Application.CustomWork.ICustomWorkPaymentService, CustomWork.CustomWorkPaymentService>();
        services.AddScoped<Application.Outstanding.IOutstandingService, Outstanding.OutstandingService>();
        services.AddScoped<Application.VendorPayments.IVendorPaymentService, VendorPayments.VendorPaymentService>();
        services.AddScoped<Application.Allocations.IAllocationEngine, Allocations.AllocationEngine>();
        services.AddScoped<Application.Allocations.IMultiProjectVendorPaymentService, Allocations.MultiProjectVendorPaymentService>();
        services.AddScoped<Application.Allocations.IAllocationHistoryService, Allocations.AllocationHistoryService>();
        services.AddScoped<Application.Integrity.IIntegrityCheckService, Integrity.IntegrityCheckService>();
        services.AddScoped<Application.Banking.IBankImportService, Banking.BankImportService>();
        services.AddScoped<Application.Banking.IBankStatementProfileService, Banking.BankStatementProfileService>();
        services.AddSingleton<Application.Banking.IBankStatementParser, Banking.BankStatementParser>();
        services.AddScoped<Application.Banking.IMatchSuggestionService, Banking.MatchSuggestionService>();
        services.AddScoped<Application.Banking.IReconciliationService, Banking.ReconciliationService>();
        services.AddScoped<Application.Budgets.IProjectBudgetService, Budgets.ProjectBudgetService>();
        services.AddScoped<Application.Reporting.IReportingService, Reporting.ReportingService>();
        services.AddScoped<Application.CommonExpenses.ICommonExpenseService, CommonExpenses.CommonExpenseService>();
        services.AddScoped<Application.CommonExpenses.ICommonExpenseAllocationService, CommonExpenses.CommonExpenseAllocationService>();
        services.AddScoped<Application.Loans.ILoanService, Loans.LoanService>();
        services.AddScoped<Application.Loans.ILoanScheduleService, Loans.LoanScheduleService>();
        services.AddScoped<Application.Loans.ILoanEmiPaymentService, Loans.LoanEmiPaymentService>();
        services.AddScoped<Application.Loans.ILoanAlertService, Loans.LoanAlertService>();
        services.AddScoped<Application.Loans.ILoanReportService, Loans.LoanReportService>();

        // Notifications (P9-T01).
        services.AddOptions<Notifications.EmailOptions>().BindConfiguration(Notifications.EmailOptions.SectionName);
        services.AddScoped<Application.Notifications.IEmailSender, Notifications.SmtpEmailSender>();
        services.AddScoped<Application.Notifications.INotificationEvaluator, Notifications.NotificationEvaluator>();
        services.AddScoped<Application.Notifications.INotificationService, Notifications.NotificationService>();
        services.AddHostedService<Notifications.NotificationBackgroundService>();

        // Report framework (P8-T01) — one executor, a runner per report, one catalog.
        services.AddScoped<Reporting.Framework.ReportExecutor>();
        services.AddSingleton<Application.Reporting.Framework.IReportExporter, Reporting.Framework.ReportExporter>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.LedgerReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ProjectExpenseReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ProjectLedgerReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ProjectLabourReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ProjectMaterialReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ProjectFinancialSummaryReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ProjectBudgetVsActualReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ProjectOutstandingReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.VendorPurchaseReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.VendorPaymentReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.VendorStatementReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.VendorOutstandingReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.FieldOfficerOutstandingReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.VendorProjectWiseStatementReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.VendorPaymentAllocationReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.SubcontractorWorkValueVsPaymentReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.SubcontractorTeamExpenseReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.SubcontractorDepartmentExpenseReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.SubcontractorProjectTeamExpenseReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.SubcontractorOutstandingReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.SubcontractorPaymentHistoryReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.SubcontractorDateWisePaymentReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.SubcontractorStatementReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.DonationProjectWiseReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.DonationTempleWiseReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.DonationAllocatedReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.DonationOutstandingReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.DonationPercentageReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.DonationPaidReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.DonationDateWiseReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.AccountStatementReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.PaymentModeReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.BankWiseReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.BankReconciliationReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.PendingReconciliationReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ExcludedTransactionReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.BankReconciliationExceptionsReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ReconciliationControlReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.CompanySummaryReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.CompanyMonthlyReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ProjectProfitabilityRankingReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.CompanyPnlReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.ExpenseCategoryAnalysisReport>();
        services.AddScoped<Application.Reporting.Framework.IReportRunner, Reporting.Framework.CompanyOutstandingReport>();
        services.AddScoped<Application.Reporting.Framework.IReportCatalog, Reporting.Framework.ReportCatalog>();
        services.AddSingleton<Application.Attachments.IFileStorage, Attachments.DiskFileStorage>();
        services.AddScoped<Application.Attachments.IAttachmentService, Attachments.AttachmentService>();

        // Purchase orders (client request, 2026-09-04).
        services.AddScoped<Application.PurchaseOrders.IPurchaseOrderService, PurchaseOrders.PurchaseOrderService>();

        // Field officers (client request, 2026-09-04).
        services.AddScoped<Application.FieldOfficers.IFieldOfficerExpenseService, FieldOfficers.FieldOfficerExpenseService>();

        services.AddScoped<Persistence.Seeding.ReferenceDataSeeder>();

        // P9-T02 — process-wide EF round-trip counter for the N+1 guard tests.
        services.AddSingleton<Diagnostics.QueryCounter>();
        services.AddSingleton<Diagnostics.QueryCountInterceptor>();

        // plan.md §3.1 — always AutoDetect the server version, never hardcode it.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options
                .UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
                    mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .AddInterceptors(
                    // Order: stamp audit columns, then reject append-only mutations,
                    // then write audit rows against the final values (plan.md §5.6, §10).
                    serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>(),
                    serviceProvider.GetRequiredService<AppendOnlyGuardInterceptor>(),
                    serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
                    serviceProvider.GetRequiredService<Diagnostics.QueryCountInterceptor>());
        });

        return services;
    }
}
