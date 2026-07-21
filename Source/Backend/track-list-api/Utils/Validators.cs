using FluentValidation;

namespace api.Utils;

public static class PasswordPolicy
{
	public static string? Validate(string? password)
	{
		if (string.IsNullOrEmpty(password))
			return "Пароль не може бути порожнім.";
		if (password.Length < 8)
			return "Пароль повинен містити щонайменше 8 символів";
		if (!password.Any(char.IsLetter))
			return "Пароль має містити хоча б одну літеру.";
		if (!password.Any(char.IsDigit))
			return "Пароль має містити хоча б одну цифру.";
		return null;
	}
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
	private static readonly HashSet<string> ReservedUsernames =
	[
		"admin", "administrator", "moderator", "mod", "system", "support", "root"
	];

	public RegisterRequestValidator()
	{
		RuleFor(x => x.Email)
			.NotEmpty()
			.WithMessage("Email не може бути порожнім.")
			.EmailAddress()
			.WithMessage("Невірний формат email.");

		RuleFor(x => x.Username)
			.NotEmpty()
			.WithMessage("Нікнейм не може бути порожнім.")
			.Must(username => !ReservedUsernames.Contains(username.ToLowerInvariant()))
			.WithMessage("Цей нікнейм зарезервовано. Будь ласка, оберіть інший.");

		RuleFor(x => x.Password)
			.NotEmpty()
			.WithMessage("Пароль не може бути порожнім.")
			.MinimumLength(8)
			.WithMessage("Пароль повинен містити щонайменше 8 символів")
			.Matches(@"[a-zA-Z]")
			.WithMessage("Пароль має містити хоча б одну літеру.")
			.Matches(@"\d")
			.WithMessage("Пароль має містити хоча б одну цифру.");

		RuleFor(x => x.ConfirmPassword)
			.NotEmpty()
			.WithMessage("Підтвердження пароля не може бути порожнім.")
			.Equal(x => x.Password)
			.WithMessage("Паролі не співпадають.");
	}
}
