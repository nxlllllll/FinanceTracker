using FluentValidation;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;

public sealed class ChangeRecurringTransactionCurrencyCommandValidator : AbstractValidator<ChangeRecurringTransactionCurrencyCommand>
{
	public ChangeRecurringTransactionCurrencyCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.RecurringTransactionId)
			.NotEmpty().WithMessage(errorMessage: "The recurring transaction cannot be empty.");

		RuleFor(expression: command => command.Currency)
			.NotEmpty().WithMessage(errorMessage: "The currency cannot be empty.")
			.Matches(expression: "^[A-Z]{3}$").WithMessage(errorMessage: "The currency must be 3 uppercase letters (e.g. 'USD').");
	}
}