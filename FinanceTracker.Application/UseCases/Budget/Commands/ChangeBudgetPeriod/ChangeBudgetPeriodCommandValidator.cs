using FluentValidation;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetPeriod;

public sealed class ChangeBudgetPeriodCommandValidator : AbstractValidator<ChangeBudgetPeriodCommand>
{
	public ChangeBudgetPeriodCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.BudgetId)
			.NotEmpty().WithMessage(errorMessage: "The budget cannot be empty.");

		RuleFor(expression: command => command.To)
			.GreaterThanOrEqualTo(expression: command => command.From).WithMessage(errorMessage: "The end date cannot be before the start date.");
	}
}
