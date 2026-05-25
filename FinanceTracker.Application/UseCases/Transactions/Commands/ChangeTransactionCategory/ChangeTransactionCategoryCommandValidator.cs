using FluentValidation;

namespace FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryCommandValidator : AbstractValidator<ChangeTransactionCategoryCommand>
{
	public ChangeTransactionCategoryCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");
		
		RuleFor(expression: command => command.TransactionId)
			.NotEmpty().WithMessage(errorMessage: "The transaction cannot be empty.");

		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");
	}
}
