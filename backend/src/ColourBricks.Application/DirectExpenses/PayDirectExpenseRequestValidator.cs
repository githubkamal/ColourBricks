using FluentValidation;

namespace ColourBricks.Application.DirectExpenses;

public sealed class PayDirectExpenseRequestValidator : AbstractValidator<PayDirectExpenseRequest>
{
    public PayDirectExpenseRequestValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.PaymentModeId).GreaterThan(0);
        RuleFor(x => x.AccountId).GreaterThan(0);
        RuleFor(x => x.ReferenceNo).MaximumLength(100);
    }
}
