using FluentValidation;

namespace ColourBricks.Application.VendorPurchases;

public sealed class RecordVendorPurchaseRequestValidator : AbstractValidator<RecordVendorPurchaseRequest>
{
    public RecordVendorPurchaseRequestValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.VendorId).GreaterThan(0);
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Total).GreaterThan(0m);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemName).NotEmpty().MaximumLength(200);
            line.RuleFor(l => l.Quantity).GreaterThan(0m);
            line.RuleFor(l => l.Unit).NotEmpty().MaximumLength(30);
            line.RuleFor(l => l.Rate).GreaterThanOrEqualTo(0m);
            line.RuleFor(l => l.TaxAmount).GreaterThanOrEqualTo(0m);
        });
        RuleFor(x => x.InvoiceNumber).MaximumLength(80);
        RuleFor(x => x.PartPayment).GreaterThan(0m).When(x => x.PartPayment is not null);
        RuleFor(x => x.PartPaymentModeId)
            .NotNull().GreaterThan(0)
            .When(x => x.PartPayment is > 0m)
            .WithMessage("A payment mode is required for the part-payment.");
    }
}
