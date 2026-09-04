using ColourBricks.Application.Payments;
using ColourBricks.Domain.Payments;
using ColourBricks.Domain.Services;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Payments;

public sealed class PaymentModeService(AppDbContext db) : IPaymentModeService
{
    public async Task<IReadOnlyList<PaymentModeDto>> ListAsync(
        bool includeInactive, CancellationToken cancellationToken)
    {
        IQueryable<PaymentMode> query = db.PaymentModes.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }

        return await query
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Name)
            .Select(m => ToDto(m))
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentModeDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        PaymentMode? mode = await db.PaymentModes.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return mode is null ? null : ToDto(mode);
    }

    public async Task<PaymentModeDto> CreateAsync(
        CreatePaymentModeRequest request, CancellationToken cancellationToken)
    {
        string norm = NameNormalizer.Normalize(request.Name);

        PaymentMode? exact = await db.PaymentModes
            .FirstOrDefaultAsync(m => m.NormalisedName == norm, cancellationToken);
        if (exact is not null)
        {
            throw new PaymentModeExactDuplicateException(exact.Id, exact.Name);
        }

        var mode = new PaymentMode
        {
            Name = request.Name.Trim(),
            NormalisedName = norm,
            RequiresAccount = request.RequiresAccount,
            RequiresReference = request.RequiresReference,
            SortOrder = request.SortOrder,
        };

        db.PaymentModes.Add(mode);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            PaymentMode? raced = await db.PaymentModes.AsNoTracking()
                .FirstOrDefaultAsync(m => m.NormalisedName == norm, cancellationToken);
            throw new PaymentModeExactDuplicateException(raced?.Id ?? 0, request.Name);
        }

        return ToDto(mode);
    }

    public async Task<PaymentModeDto?> UpdateAsync(
        long id, UpdatePaymentModeRequest request, CancellationToken cancellationToken)
    {
        PaymentMode? mode = await db.PaymentModes
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (mode is null)
        {
            return null;
        }

        string norm = NameNormalizer.Normalize(request.Name);
        db.Entry(mode).Property(m => m.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

        mode.Name = request.Name.Trim();
        mode.NormalisedName = norm;
        mode.RequiresAccount = request.RequiresAccount;
        mode.RequiresReference = request.RequiresReference;
        mode.SortOrder = request.SortOrder;
        mode.IsActive = request.IsActive;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            PaymentMode? clash = await db.PaymentModes.AsNoTracking()
                .FirstOrDefaultAsync(m => m.NormalisedName == norm && m.Id != id, cancellationToken);
            throw new PaymentModeExactDuplicateException(clash?.Id ?? 0, request.Name);
        }

        return ToDto(mode);
    }

    public async Task ValidateInstructionAsync(
        PaymentInstruction instruction, CancellationToken cancellationToken)
    {
        PaymentMode? mode = await db.PaymentModes.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == instruction.PaymentModeId, cancellationToken);

        if (mode is null || !mode.IsActive)
        {
            throw new ValidationException(
                [new ValidationFailure("paymentModeId", "Select an active payment mode.")]);
        }

        var failures = new List<ValidationFailure>();

        if (mode.RequiresReference && string.IsNullOrWhiteSpace(instruction.ReferenceNo))
        {
            failures.Add(new ValidationFailure(
                "referenceNo", $"{mode.Name} payments require a reference number."));
        }

        if (mode.RequiresAccount && instruction.AccountId is null)
        {
            failures.Add(new ValidationFailure(
                "accountId", $"{mode.Name} payments require a cash/bank account."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    private static PaymentModeDto ToDto(PaymentMode m) => new(
        m.Id, m.Name, m.RequiresAccount, m.RequiresReference, m.SortOrder, m.IsActive, m.ConcurrencyStamp);
}
