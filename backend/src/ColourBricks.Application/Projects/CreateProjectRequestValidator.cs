using FluentValidation;

namespace ColourBricks.Application.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Code)
            .MaximumLength(30)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Code may contain letters, digits and hyphens only.")
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        // BRD §70 rule 2 — every project has an estimated/approved cost.
        RuleFor(x => x.EstimatedCost)
            .NotNull().WithMessage("Estimated cost is required.")
            .Must(value => value is null or >= 0m).WithMessage("Estimated cost cannot be negative.");

        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0);

        RuleFor(x => x.ExpectedEndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Expected completion date cannot be before the start date.")
            .When(x => x.ExpectedEndDate is not null);

        RuleFor(x => x.ActualEndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Actual completion date cannot be before the start date.")
            .When(x => x.ActualEndDate is not null);

        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();

        RuleFor(x => x.ExpectedEndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Expected completion date cannot be before the start date.")
            .When(x => x.ExpectedEndDate is not null);

        RuleFor(x => x.ActualEndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Actual completion date cannot be before the start date.")
            .When(x => x.ActualEndDate is not null);
    }
}
