using FluentValidation;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryCommandValidator : AbstractValidator<ChangeTransactionCategoryCommand>
{
	public ChangeTransactionCategoryCommandValidator()
	{
		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");
	}
}