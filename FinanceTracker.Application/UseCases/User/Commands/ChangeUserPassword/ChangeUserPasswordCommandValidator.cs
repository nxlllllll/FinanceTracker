using FluentValidation;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
	public ChangeUserPasswordCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.CurrentSessionId)
			.NotEmpty().WithMessage(errorMessage: "The current session cannot be empty.");

		RuleFor(expression: command => command.CurrentPassword)
			.NotEmpty().WithMessage(errorMessage: "The current password cannot be empty.")
			.MaximumLength(maximumLength: 128).WithMessage(errorMessage: "The current password must not exceed 128 characters.");

		RuleFor(expression: command => command.NewPassword)
			.NotEmpty().WithMessage(errorMessage: "The password cannot be empty.")
			.MinimumLength(minimumLength: 8).WithMessage(errorMessage: "The password must be at least 8 characters.")
			.MaximumLength(maximumLength: 128).WithMessage(errorMessage: "The password must not exceed 128 characters.");
	}
}
