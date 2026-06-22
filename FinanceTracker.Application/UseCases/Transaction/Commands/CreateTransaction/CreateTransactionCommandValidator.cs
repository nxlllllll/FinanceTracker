using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FluentValidation;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;

public sealed class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
	public CreateTransactionCommandValidator(
		IDateProvider dateProvider,
		ICurrencyReadRepository currencyReadRepository,
		ICategoryReadRepository categoryReadRepository)
	{
		RuleFor(expression: command => command.AccountId)
			.NotEmpty().WithMessage(errorMessage: "The account cannot be empty.");

		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.CategoryId)
			.Cascade(cascadeMode: CascadeMode.Stop)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.")
			.MustBelongToUser(
				existsForUserAsync: categoryReadRepository.ExistsAsync,
				userIdSelector: command => command.UserId,
				entityName: "category"
			);
		
		RuleFor(expression: command => command.Currency)
			.MustAsync(predicate: async (currency, ct) => await currencyReadRepository.ExistsAsync(code: currency.Value, ct: ct))
			.WithMessage(errorMessage: "The currency code does not exist.");
		
		RuleFor(expression: command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The transaction amount must be greater than zero.");
		
		RuleFor(expression: command => command.Direction)
			.IsInEnum().WithMessage(errorMessage: "The direction type can only be 'Credit' or 'Debit'.");

		RuleFor(expression: command => command.Description)
			.MaximumLength(maximumLength: 255).WithMessage(errorMessage: "The description cannot exceed 255 characters.")
			.When(predicate: command => command.Description is not null);
		
		RuleFor(expression: command => command.OccurredAt) 
			.NotEmpty().WithMessage(errorMessage: "The transaction date cannot be empty.")
			.Must(occurredAt => occurredAt <= dateProvider.UtcNow)
			.WithMessage(errorMessage: "The transaction date cannot be in the future.");
	}
}