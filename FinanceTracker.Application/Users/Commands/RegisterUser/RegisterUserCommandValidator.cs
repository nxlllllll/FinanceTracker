using FluentValidation;

namespace FinanceTracker.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
	public RegisterUserCommandValidator()
	{
		RuleFor(expression: command => command.Email)
			.NotEmpty().WithMessage(errorMessage: "The email cannot be empty.")
			.EmailAddress().WithMessage(errorMessage: "The email is invalid.");

		RuleFor(expression: command => command.PasswordHash)
			.NotEmpty().WithMessage(errorMessage: "The password hash cannot be empty.");

		RuleFor(expression: command => command.BaseCurrencyCode)
			.NotEmpty().WithMessage(errorMessage: "The base currency code cannot be empty.")
			.Length(exactLength: 3).WithMessage(errorMessage: "The base currency code must be 3 characters.");
	}
}