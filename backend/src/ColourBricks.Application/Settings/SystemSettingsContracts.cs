namespace ColourBricks.Application.Settings;

public sealed record SystemSettingsDto(
    long Id,
    string CompanyName,
    string? CompanyAddress,
    string? CompanyGstin,
    string? CompanyLogoUrl,
    long? CompanyLogoAttachmentId,
    decimal VendorOutstandingAlertLimit,
    int OverdueAlertDays,
    decimal ProfitFloorAlertPercent,
    int LoanEmiReminderDaysAhead,
    string ConcurrencyStamp);

public sealed record UpdateSystemSettingsRequest(
    string CompanyName,
    string? CompanyAddress,
    string? CompanyGstin,
    string? CompanyLogoUrl,
    long? CompanyLogoAttachmentId,
    decimal VendorOutstandingAlertLimit,
    int OverdueAlertDays,
    decimal ProfitFloorAlertPercent,
    int LoanEmiReminderDaysAhead,
    string ConcurrencyStamp);

public interface ISystemSettingsService
{
    /// <summary>Reads the one settings row, creating it with defaults if this is the first read.</summary>
    Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the settings row. A stale <c>ConcurrencyStamp</c> on
    /// <paramref name="request"/> throws <c>DbUpdateConcurrencyException</c>, mapped
    /// to a 409 by the global handler (plan.md §6).
    /// </summary>
    Task<SystemSettingsDto> UpdateAsync(UpdateSystemSettingsRequest request, CancellationToken cancellationToken);
}
