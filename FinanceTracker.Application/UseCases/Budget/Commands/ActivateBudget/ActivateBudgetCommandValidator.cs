using FluentValidation;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;

public sealed class ActivateBudgetCommandValidator : AbstractValidator<ActivateBudgetCommand>
{
	public ActivateBudgetCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.BudgetId)
			.NotEmpty().WithMessage(errorMessage: "The budget cannot be empty.");
	}
}
