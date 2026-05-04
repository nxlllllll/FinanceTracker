using FluentValidation;

namespace FinanceTracker.Application.UseCases.Accounts.Commands.UnarchiveAccount;

public sealed class UnarchiveAccountCommandValidator : AbstractValidator<UnarchiveAccountCommand>
{
	public UnarchiveAccountCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.AccountId)
			.NotEmpty().WithMessage(errorMessage: "The account cannot be empty.");
	}
}