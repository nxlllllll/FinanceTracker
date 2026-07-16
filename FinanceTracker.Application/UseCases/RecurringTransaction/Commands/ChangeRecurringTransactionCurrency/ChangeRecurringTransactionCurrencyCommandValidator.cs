using FinanceTracker.Core.Repositories.Currency;
using FluentValidation;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;

public sealed class ChangeRecurringTransactionCurrencyCommandValidator : AbstractValidator<ChangeRecurringTransactionCurrencyCommand>
{
	public ChangeRecurringTransactionCurrencyCommandValidator(ICurrencyReadRepository currencyReadRepository)
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.RecurringTransactionId)
			.NotEmpty().WithMessage(errorMessage: "The recurring transaction cannot be empty.");

		RuleFor(expression: command => command.Currency)
			.MustAsync(predicate: async (currency, ct) => await currencyReadRepository.ExistsAsync(code: currency.Value, ct: ct))
			.WithMessage(errorMessage: "The currency code does not exist.");
	}
}
