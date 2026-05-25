using FluentValidation;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed class ChangeRecurringTransactionDayOfMonthCommandValidator : AbstractValidator<ChangeRecurringTransactionDayOfMonthCommand>
{
	public ChangeRecurringTransactionDayOfMonthCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.RecurringTransactionId)
			.NotEmpty().WithMessage(errorMessage: "The recurring transaction cannot be empty.");

		RuleFor(expression: command => command.DayOfMonth)
			.InclusiveBetween(from: 1, to: 31).WithMessage(errorMessage: "Day of month must be between 1 and 31.");
	}
}
