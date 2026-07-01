using FinanceTracker.Application.Configurations.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;

public sealed class ChangeBudgetAmountCommandValidator : AbstractValidator<ChangeBudgetAmountCommand>
{
	public ChangeBudgetAmountCommandValidator(IOptionsMonitor<MoneyLimitsOptions> moneyLimits)
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.BudgetId)
			.NotEmpty().WithMessage(errorMessage: "The budget cannot be empty.");

		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The amount must be greater than 0.")
			.LessThanOrEqualTo(valueToCompare: moneyLimits.CurrentValue.MaxAmount)
			.WithMessage(errorMessage: $"The amount cannot exceed {moneyLimits.CurrentValue.MaxAmount:N2}.");
	}
}