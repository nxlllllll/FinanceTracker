using FluentValidation;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
	public ChangeUserPasswordCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.NewPassword)
			.NotEmpty().WithMessage(errorMessage: "The password cannot be empty.")
			.MinimumLength(minimumLength: 8).WithMessage(errorMessage: "The password must be at least 8 characters.");
	}
}
