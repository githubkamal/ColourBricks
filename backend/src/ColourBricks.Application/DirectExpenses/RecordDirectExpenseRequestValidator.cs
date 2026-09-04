using FluentValidation;

namespace ColourBricks.Application.DirectExpenses;

public sealed class RecordDirectExpenseRequestValidator : AbstractValidator<RecordDirectExpenseRequest>
{
    public RecordDirectExpenseRequestValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("An expense category is required.");
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.PaymentModeId)
            .NotNull().GreaterThan(0)
            .When(x => x.PaidImmediately)
            .WithMessage("A payment mode is required for an immediately paid expense.");
    }
}
