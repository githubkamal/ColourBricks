using FluentValidation;

namespace ColourBricks.Application.Payments;

public sealed class CreatePaymentModeRequestValidator : AbstractValidator<CreatePaymentModeRequest>
{
    public CreatePaymentModeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
    }
}

public sealed class UpdatePaymentModeRequestValidator : AbstractValidator<UpdatePaymentModeRequest>
{
    public UpdatePaymentModeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
    }
}
