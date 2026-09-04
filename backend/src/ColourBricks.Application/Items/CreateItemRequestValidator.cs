using FluentValidation;

namespace ColourBricks.Application.Items;

public sealed class CreateItemRequestValidator : AbstractValidator<CreateItemRequest>
{
    public CreateItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DefaultRate)
            .GreaterThanOrEqualTo(0m).WithMessage("Default rate cannot be negative.");
        RuleFor(x => x.TaxRate)
            .InclusiveBetween(0m, 100m).WithMessage("Tax rate must be between 0 and 100.");
    }
}

public sealed class UpdateItemRequestValidator : AbstractValidator<UpdateItemRequest>
{
    public UpdateItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DefaultRate)
            .GreaterThanOrEqualTo(0m).WithMessage("Default rate cannot be negative.");
        RuleFor(x => x.TaxRate)
            .InclusiveBetween(0m, 100m).WithMessage("Tax rate must be between 0 and 100.");
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
    }
}

public sealed class CreateItemCategoryRequestValidator : AbstractValidator<CreateItemCategoryRequest>
{
    public CreateItemCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateUnitRequestValidator : AbstractValidator<CreateUnitRequest>
{
    public CreateUnitRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
    }
}
