using FluentValidation;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;

public sealed class ActivateRecurringTransactionCommandValidator : AbstractValidator<ActivateRecurringTransactionCommand>
{
	public ActivateRecurringTransactionCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.RecurringTransactionId)
			.NotEmpty().WithMessage(errorMessage: "The recurring transaction cannot be empty.");
	}
}