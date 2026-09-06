using FluentValidation;

namespace ColourBricks.Application.VendorPayments;

public sealed class RecordVendorPaymentRequestValidator : AbstractValidator<RecordVendorPaymentRequest>
{
    public RecordVendorPaymentRequestValidator()
    {
        RuleFor(x => x.VendorId).GreaterThan(0);
        // No project = the whole amount is carried as a vendor advance (BRD §25) —
        // optional by client request (2026-09-06), same "not mandatory but available"
        // rule as the bank-transaction link.
        RuleFor(x => x.ProjectId).GreaterThan(0).When(x => x.ProjectId.HasValue);
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.PaymentModeId).GreaterThan(0);
        RuleFor(x => x.ReferenceNo).MaximumLength(80);
    }
}
