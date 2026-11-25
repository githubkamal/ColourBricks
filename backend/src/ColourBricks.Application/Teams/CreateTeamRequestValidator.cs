using FluentValidation;

namespace ColourBricks.Application.Teams;

public sealed class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("A department is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.AlternatePhone).MaximumLength(40);
        RuleFor(x => x.WorkingArea).MaximumLength(500);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.BankAccountHolderName).MaximumLength(200);
        RuleFor(x => x.BankName).MaximumLength(200);
        RuleFor(x => x.BankAccountNumber).MaximumLength(40)
            .Matches("^[0-9A-Za-z]*$").WithMessage("Account number may contain only letters and digits.");
        RuleFor(x => x.BankIfsc).MaximumLength(20)
            .Matches("^[A-Za-z]{4}0[A-Za-z0-9]{6}$").WithMessage("Enter a valid 11-character IFSC code.")
            .When(x => !string.IsNullOrWhiteSpace(x.BankIfsc));
        RuleForEach(x => x.Sites).SetValidator(new TeamSiteInputValidator());
    }
}

public sealed class UpdateTeamRequestValidator : AbstractValidator<UpdateTeamRequest>
{
    public UpdateTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("A department is required.");
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.AlternatePhone).MaximumLength(40);
        RuleFor(x => x.WorkingArea).MaximumLength(500);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.BankAccountHolderName).MaximumLength(200);
        RuleFor(x => x.BankName).MaximumLength(200);
        RuleFor(x => x.BankAccountNumber).MaximumLength(40)
            .Matches("^[0-9A-Za-z]*$").WithMessage("Account number may contain only letters and digits.");
        RuleFor(x => x.BankIfsc).MaximumLength(20)
            .Matches("^[A-Za-z]{4}0[A-Za-z0-9]{6}$").WithMessage("Enter a valid 11-character IFSC code.")
            .When(x => !string.IsNullOrWhiteSpace(x.BankIfsc));
        RuleForEach(x => x.Sites).SetValidator(new TeamSiteInputValidator());
    }
}

public sealed class TeamSiteInputValidator : AbstractValidator<TeamSiteInput>
{
    public TeamSiteInputValidator()
    {
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.SiteLocation).MaximumLength(200);
        RuleFor(x => x)
            .Must(x => x.ProjectId is > 0 || !string.IsNullOrWhiteSpace(x.SiteLocation))
            .WithName("site")
            .WithMessage("Each site needs a project or a site location.");
    }
}
