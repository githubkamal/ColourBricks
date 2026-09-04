using FluentValidation;

namespace ColourBricks.Application.Receipts;

public sealed class RecordReceiptRequestValidator : AbstractValidator<RecordReceiptRequest>
{
    public RecordReceiptRequestValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0).WithMessage("A project is required (exactly one).");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.PaymentModeId).GreaterThan(0);
        RuleFor(x => x.ReferenceNo).MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
