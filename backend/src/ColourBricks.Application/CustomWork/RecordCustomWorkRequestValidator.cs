using FluentValidation;

namespace ColourBricks.Application.CustomWork;

public sealed class RecordCustomWorkRequestValidator : AbstractValidator<RecordCustomWorkRequest>
{
    public RecordCustomWorkRequestValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.ActualCost).GreaterThan(0m).WithMessage("Actual cost is required to post custom work.");
        RuleFor(x => x.WorkType).MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
