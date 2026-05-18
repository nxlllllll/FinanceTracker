using FluentValidation;

namespace FinanceTracker.Application.UseCases.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
	public RegisterUserCommandValidator()
	{
		RuleFor(expression: command => command.Email)
			.NotEmpty().WithMessage(errorMessage: "The email cannot be empty.")
			.EmailAddress().WithMessage(errorMessage: "The email is invalid.");

		RuleFor(expression: command => command.Password)
			.NotEmpty().WithMessage(errorMessage: "The password cannot be empty.")
			.MinimumLength(minimumLength: 8).WithMessage(errorMessage: "The password must be at least 8 characters.");

		RuleFor(expression: command => command.BaseCurrencyCode)
			.NotEmpty().WithMessage(errorMessage: "The base currency code cannot be empty.")
			.Length(exactLength: 3).WithMessage(errorMessage: "The base currency code must be 3 characters.");
	}
}