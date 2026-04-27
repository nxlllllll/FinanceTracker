using FluentValidation;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;

public sealed class ChangeBudgetAmountCommandValidator : AbstractValidator<ChangeBudgetAmountCommand>
{
	public ChangeBudgetAmountCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.BudgetId)
			.NotEmpty().WithMessage(errorMessage: "The budget cannot be empty.");

		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The amount must be greater than 0.");
	}
}