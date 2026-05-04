using FluentValidation;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
	public ChangeUserPasswordCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.NewPasswordHash)
			.NotEmpty().WithMessage(errorMessage: "The password hash cannot be empty.");
	}
}