using FluentValidation;

namespace ColourBricks.Application.Labour;

public sealed class RecordWorkRequestValidator : AbstractValidator<RecordWorkRequest>
{
    public RecordWorkRequestValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.TeamId).GreaterThan(0);
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.AgreedValue).GreaterThan(0m);
        RuleFor(x => x.WorkType).MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class PayWorkRequestValidator : AbstractValidator<PayWorkRequest>
{
    public PayWorkRequestValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.PaymentModeId).GreaterThan(0);
        RuleFor(x => x.ReferenceNo).MaximumLength(80);
    }
}
