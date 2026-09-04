using FluentValidation;

namespace ColourBricks.Application.Donations;

public sealed class UpsertProjectDonationRequestValidator : AbstractValidator<UpsertProjectDonationRequest>
{
    public UpsertProjectDonationRequestValidator()
    {
        RuleFor(x => x.Basis).IsInEnum();
        RuleFor(x => x.Temples).NotNull();
        // Amount arithmetic (split totals the donation, valid temples, basis inputs)
        // is checked in ProjectDonationService against the project's contract value.
    }
}
