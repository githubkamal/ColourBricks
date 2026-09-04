using ColourBricks.Application.Settings;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Settings;

public sealed class SystemSettingsService(AppDbContext db) : ISystemSettingsService
{
    public async Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await LoadOrCreateAsync(cancellationToken));

    public async Task<SystemSettingsDto> UpdateAsync(
        UpdateSystemSettingsRequest request, CancellationToken cancellationToken)
    {
        Domain.Settings.SystemSettings settings = await LoadOrCreateAsync(cancellationToken);

        db.Entry(settings).Property(s => s.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        settings.CompanyName = request.CompanyName.Trim();
        settings.CompanyAddress = request.CompanyAddress?.Trim();
        settings.CompanyGstin = request.CompanyGstin?.Trim();
        settings.CompanyLogoUrl = request.CompanyLogoUrl?.Trim();
        settings.VendorOutstandingAlertLimit = request.VendorOutstandingAlertLimit;
        settings.OverdueAlertDays = request.OverdueAlertDays;
        settings.ProfitFloorAlertPercent = request.ProfitFloorAlertPercent;
        settings.LoanEmiReminderDaysAhead = request.LoanEmiReminderDaysAhead;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    private async Task<Domain.Settings.SystemSettings> LoadOrCreateAsync(CancellationToken cancellationToken)
    {
        Domain.Settings.SystemSettings? settings =
            await db.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new Domain.Settings.SystemSettings();
        db.SystemSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static SystemSettingsDto ToDto(Domain.Settings.SystemSettings s) => new(
        s.CompanyName, s.CompanyAddress, s.CompanyGstin, s.CompanyLogoUrl,
        s.VendorOutstandingAlertLimit, s.OverdueAlertDays, s.ProfitFloorAlertPercent,
        s.LoanEmiReminderDaysAhead, s.ConcurrencyStamp);
}
