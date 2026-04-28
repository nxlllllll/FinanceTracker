using FluentValidation;

namespace FinanceTracker.Application.Budgets.Commands.CreateBudget;

public sealed class CreateBudgetCommandValidator : AbstractValidator<CreateBudgetCommand>
{
	public CreateBudgetCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");

		RuleFor(expression: command => command.Currency)
			.NotEmpty().WithMessage(errorMessage: "The currency cannot be empty.")
			.Length(exactLength: 3).WithMessage(errorMessage: "The currency code must be 3 characters.");

		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The amount must be greater than 0.");

		RuleFor(expression: command => command.To)
			.GreaterThan(expression: command => command.From).WithMessage(errorMessage: "The end date must be after the start date.");
	}
}