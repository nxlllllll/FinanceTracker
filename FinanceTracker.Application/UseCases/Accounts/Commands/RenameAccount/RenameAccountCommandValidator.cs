using FluentValidation;

namespace FinanceTracker.Application.UseCases.Accounts.Commands.RenameAccount;

public sealed class RenameAccountCommandValidator : AbstractValidator<RenameAccountCommand>
{
	public RenameAccountCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.AccountId)
			.NotEmpty().WithMessage(errorMessage: "The account cannot be empty.");
		
		RuleFor(expression: command => command.NewName)
			.NotEmpty().WithMessage("The new name cannot be empty.");
	}
}