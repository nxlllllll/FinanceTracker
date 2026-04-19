using FluentValidation;

namespace FinanceTracker.Application.Accounts.Commands.RenameAccount;

public sealed class RenameAccountCommandValidator : AbstractValidator<RenameAccountCommand>
{
	public RenameAccountCommandValidator()
	{		
		RuleFor(expression: command => command.NewName)
			.NotEmpty().WithMessage("The new name cannot be empty.");
	}
}