using FluentValidation;

namespace ColourBricks.Application.VendorPayments;

public sealed class RecordVendorPaymentRequestValidator : AbstractValidator<RecordVendorPaymentRequest>
{
    public RecordVendorPaymentRequestValidator()
    {
        RuleFor(x => x.VendorId).GreaterThan(0);
        RuleFor(x => x.ProjectId).GreaterThan(0).WithMessage("A single project is required for this payment.");
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.PaymentModeId).GreaterThan(0);
        RuleFor(x => x.ReferenceNo).MaximumLength(80);
    }
}
