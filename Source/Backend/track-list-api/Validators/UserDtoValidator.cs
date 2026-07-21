using FluentValidation;

namespace api.Validators;

public class UserDtoValidator : AbstractValidator<UserDto>
{
    public UserDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Length(3, 40).WithMessage("Username must be between 3 and 40 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        // Password only required when provided (registration flow).
        // Profile update does not send password.
        When(x => !string.IsNullOrEmpty(x.Password), () =>
        {
            RuleFor(x => x.Password)
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .Matches("[A-Za-zА-Яа-яЁёІіЇїЄєҐґ]").WithMessage("Password must contain at least one letter.")
                .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
        });

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid role.");

        RuleFor(x => x.Gender)
            .IsInEnum().WithMessage("Invalid gender.");

        // Optional image validation
        When(x => x.ProfilePic is not null, () =>
        {
            RuleFor(x => x.ProfilePic!.Length)
                .LessThanOrEqualTo(1024 * 100) // 100 кб
                .WithMessage("Profile picture file must be less than 5MB.");

            RuleFor(x => x.ProfilePic!.ContentType)
                .Must(ct => ct is "image/jpeg" or "image/png")
                .WithMessage("Only JPEG and PNG images are allowed.");
        });
    }
}