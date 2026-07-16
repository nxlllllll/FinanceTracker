using FluentValidation;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;

public sealed class ChangeUserEmailCommandValidator : AbstractValidator<ChangeUserEmailCommand>
{
	public ChangeUserEmailCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.CurrentSessionId)
			.NotEmpty().WithMessage(errorMessage: "The current session cannot be empty.");

		RuleFor(expression: command => command.CurrentPassword)
			.NotEmpty().WithMessage(errorMessage: "The current password cannot be empty.")
			.MaximumLength(maximumLength: 128).WithMessage(errorMessage: "The current password must not exceed 128 characters.");

		RuleFor(expression: command => command.NewEmail)
			.NotEmpty().WithMessage(errorMessage: "The email cannot be empty.")
			.EmailAddress().WithMessage(errorMessage: "The email is invalid.");
	}
}
