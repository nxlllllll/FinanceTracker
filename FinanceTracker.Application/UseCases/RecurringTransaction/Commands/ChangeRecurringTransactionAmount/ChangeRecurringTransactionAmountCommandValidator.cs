using FinanceTracker.Application.Configurations.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;

public sealed class ChangeRecurringTransactionAmountCommandValidator : AbstractValidator<ChangeRecurringTransactionAmountCommand>
{
	public ChangeRecurringTransactionAmountCommandValidator(IOptionsMonitor<MoneyLimitsOptions> moneyLimits)
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.RecurringTransactionId)
			.NotEmpty().WithMessage(errorMessage: "The recurring transaction cannot be empty.");

		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The amount must be greater than zero.")
			.LessThanOrEqualTo(valueToCompare: moneyLimits.CurrentValue.MaxAmount)
			.WithMessage(errorMessage: $"The amount cannot exceed {moneyLimits.CurrentValue.MaxAmount:N2}.");
	}
}