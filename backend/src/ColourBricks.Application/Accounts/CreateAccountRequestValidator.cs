using FluentValidation;

namespace ColourBricks.Application.Accounts;

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.OpeningBalanceDate).NotEmpty();
        RuleFor(x => x.AccountNumber).MaximumLength(40);
        RuleFor(x => x.Ifsc).MaximumLength(20);
        RuleFor(x => x.BankName).MaximumLength(120);
    }
}

public sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.OpeningBalanceDate).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.AccountNumber).MaximumLength(40);
        RuleFor(x => x.Ifsc).MaximumLength(20);
        RuleFor(x => x.BankName).MaximumLength(120);
    }
}
