using FluentValidation;

namespace FinanceTracker.Application.Users.Commands.ChangeUserEmail;

public sealed class ChangeUserEmailCommandValidator : AbstractValidator<ChangeUserEmailCommand>
{
	public ChangeUserEmailCommandValidator()
	{
		RuleFor(expression: command => command.NewEmail)
			.NotEmpty().WithMessage(errorMessage: "The email cannot be empty.")
			.EmailAddress().WithMessage(errorMessage: "The email is invalid.");
	}
}