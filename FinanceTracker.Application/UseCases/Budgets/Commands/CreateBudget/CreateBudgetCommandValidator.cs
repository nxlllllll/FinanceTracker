using FinanceTracker.Core.Repositories.Currency;
using FluentValidation;

namespace FinanceTracker.Application.UseCases.Budgets.Commands.CreateBudget;

public sealed class CreateBudgetCommandValidator : AbstractValidator<CreateBudgetCommand>
{
	public CreateBudgetCommandValidator(ICurrencyReadRepository currencyReadRepository)
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");

		RuleFor(expression: command => command.Currency)
			.MustAsync(predicate: async (currency, ct) => await currencyReadRepository.ExistsAsync(code: currency.Value, ct: ct))
			.WithMessage(errorMessage: "The currency code does not exist.");

		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The amount must be greater than 0.");

		RuleFor(expression: command => command.To)
			.GreaterThan(expression: command => command.From).WithMessage(errorMessage: "The end date must be after the start date.");
	}
}
