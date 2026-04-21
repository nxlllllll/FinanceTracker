using FluentValidation;

namespace FinanceTracker.Application.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
	public CreateTransactionCommandValidator()
	{
		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The transaction amount must be greater than zero.");

		RuleFor(expression: command => command.ExchangeRate)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The exchange rate must be greater than zero.");

		RuleFor(expression: command => command.Direction)
			.IsInEnum().WithMessage(errorMessage: "The direction type can only be 'Credit' or 'Debit'.");

		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");
		
		RuleFor(expression: command => command.OccurredAt) 
			.NotEmpty().WithMessage(errorMessage: "The transaction date cannot be empty.")
			.Must(occurredAt => occurredAt <= DateTime.UtcNow).WithMessage(errorMessage: "The transaction date cannot be in the future.");
		
		RuleFor(expression: command => command.Description)
			.MaximumLength(maximumLength: 255).WithMessage(errorMessage: "The description cannot exceed 255 characters.")
			.When(predicate: command => command.Description is not null);
	}
}