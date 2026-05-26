using FluentValidation;

namespace FinanceTracker.Application.UseCases.Budget.Commands.DeleteBudget;

public sealed class DeleteBudgetCommandValidator : AbstractValidator<DeleteBudgetCommand>
{
	public DeleteBudgetCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.BudgetId)
			.NotEmpty().WithMessage(errorMessage: "The budget cannot be empty.");
	}
}
