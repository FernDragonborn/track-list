using api.DTOs;
using FluentValidation;

namespace api.Validators;

public class CreateCollectionRequestValidator : AbstractValidator<CreateCollectionRequest>
{
    public CreateCollectionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Collection name is required.")
            .MaximumLength(200).WithMessage("Collection name must be at most 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be at most 1000 characters.");

        RuleFor(x => x.PrivacyLevel)
            .IsInEnum().WithMessage("Invalid privacy level.");
    }
}

public class UpdateCollectionRequestValidator : AbstractValidator<UpdateCollectionRequest>
{
    public UpdateCollectionRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Collection name must be at most 200 characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be at most 1000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.PrivacyLevel)
            .IsInEnum().WithMessage("Invalid privacy level.")
            .When(x => x.PrivacyLevel is not null);
    }
}
