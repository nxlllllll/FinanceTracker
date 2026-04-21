using FluentValidation;

namespace FinanceTracker.Application.Users.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
	public ChangeUserPasswordCommandValidator()
	{
		RuleFor(expression: command => command.NewPasswordHash)
			.NotEmpty().WithMessage(errorMessage: "The password hash cannot be empty.");
	}
}