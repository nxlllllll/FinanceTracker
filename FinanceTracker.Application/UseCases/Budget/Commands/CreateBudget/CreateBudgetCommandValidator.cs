using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Currency;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;

public sealed class CreateBudgetCommandValidator : AbstractValidator<CreateBudgetCommand>
{
	public CreateBudgetCommandValidator(
		ICurrencyReadRepository currencyReadRepository,
		ICategoryReadRepository categoryReadRepository,
		IOptionsMonitor<MoneyLimitsOptions> moneyLimits)
	{
		RuleFor(expression: command => command.UserId)
		.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.CategoryId).Cascade(cascadeMode: CascadeMode.Stop)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.")
			.MustBelongToUser(
				existsForUserAsync: categoryReadRepository.ExistsAsync,
				userIdSelector: command => command.UserId,
				entityName: "category"
			);

		RuleFor(expression: command => command.Currency)
			.MustAsync(predicate: async (currency, ct) => await currencyReadRepository.ExistsAsync(code: currency.Value, ct: ct))
			.WithMessage(errorMessage: "The currency code does not exist.");

		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The amount must be greater than 0.")
			.LessThanOrEqualTo(valueToCompare: moneyLimits.CurrentValue.MaxAmount)
			.WithMessage(errorMessage: $"The amount cannot exceed {moneyLimits.CurrentValue.MaxAmount:N2}.");

		RuleFor(expression: command => command.To)
			.GreaterThan(expression: command => command.From).WithMessage(errorMessage: "The end date must be after the start date.");
	}
}
