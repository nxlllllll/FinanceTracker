using FluentValidation;

namespace FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;

public sealed class ArchiveAccountCommandValidator : AbstractValidator<ArchiveAccountCommand>
{
	public ArchiveAccountCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.AccountId)
			.NotEmpty().WithMessage(errorMessage: "The account cannot be empty.");
	}
}
