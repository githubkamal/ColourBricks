using FluentValidation;

namespace ColourBricks.Application.Parties;

public sealed class CreatePartyRequestValidator : AbstractValidator<CreatePartyRequest>
{
    public CreatePartyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Types).NotEmpty().WithMessage("At least one party type is required.");
        RuleForEach(x => x.Types).IsInEnum();

        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.GstNumber).MaximumLength(20);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.ContactPerson).MaximumLength(120);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}
