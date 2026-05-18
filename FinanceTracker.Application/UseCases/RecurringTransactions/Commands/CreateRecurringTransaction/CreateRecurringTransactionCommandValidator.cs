using FinanceTracker.Core.Repositories.Currency;
using FluentValidation;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionCommandValidator : AbstractValidator<CreateRecurringTransactionCommand>
{
	public CreateRecurringTransactionCommandValidator(ICurrencyReadRepository currencyReadRepository)
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.AccountId)
			.NotEmpty().WithMessage(errorMessage: "The account cannot be empty.");

		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");

		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The amount must be greater than zero.");

		RuleFor(expression: command => command.Currency)
			.MustAsync(predicate: async (currency, ct) => await currencyReadRepository.ExistsAsync(code: currency.Value, ct: ct))
			.WithMessage(errorMessage: "The currency code does not exist.");

		RuleFor(expression: command => command.Direction)
			.IsInEnum().WithMessage(errorMessage: "The direction type can only be 'Credit' or 'Debit'.");

		RuleFor(expression: command => command.DayOfMonth)
			.InclusiveBetween(from: 1, to: 31).WithMessage(errorMessage: "Day of month must be between 1 and 31.");

		RuleFor(expression: command => command.Description)
			.MaximumLength(maximumLength: 255).WithMessage(errorMessage: "The description cannot exceed 255 characters.")
			.When(predicate: command => command.Description is not null);
	}
}