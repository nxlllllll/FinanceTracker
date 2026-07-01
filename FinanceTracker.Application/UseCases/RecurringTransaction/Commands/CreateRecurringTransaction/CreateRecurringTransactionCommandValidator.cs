using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Currency;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionCommandValidator : AbstractValidator<CreateRecurringTransactionCommand>
{
	public CreateRecurringTransactionCommandValidator(
		ICurrencyReadRepository currencyReadRepository,
		ICategoryReadRepository categoryReadRepository,
		IOptionsMonitor<MoneyLimitsOptions> moneyLimits)
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.AccountId)
			.NotEmpty().WithMessage(errorMessage: "The account cannot be empty.");

		RuleFor(expression: command => command.CategoryId)
			.Cascade(cascadeMode: CascadeMode.Stop)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.")
			.MustBelongToUser(
				existsForUserAsync: categoryReadRepository.ExistsAsync,
				userIdSelector: command => command.UserId,
				entityName: "category"
			);

		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The amount must be greater than zero.")
			.LessThanOrEqualTo(valueToCompare: moneyLimits.CurrentValue.MaxAmount)
			.WithMessage(errorMessage: $"The amount cannot exceed {moneyLimits.CurrentValue.MaxAmount:N2}.");

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