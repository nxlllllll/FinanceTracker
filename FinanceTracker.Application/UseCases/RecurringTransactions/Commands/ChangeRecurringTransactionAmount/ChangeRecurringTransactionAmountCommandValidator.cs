using FluentValidation;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;

public sealed class ChangeRecurringTransactionAmountCommandValidator : AbstractValidator<ChangeRecurringTransactionAmountCommand>
{
	public ChangeRecurringTransactionAmountCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.RecurringTransactionId)
			.NotEmpty().WithMessage(errorMessage: "The recurring transaction cannot be empty.");

		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The amount must be greater than zero.");
	}
}
