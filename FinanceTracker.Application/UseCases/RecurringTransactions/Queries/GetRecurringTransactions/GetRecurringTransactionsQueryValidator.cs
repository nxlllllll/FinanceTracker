using FluentValidation;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Queries.GetRecurringTransactions;

public sealed class GetRecurringTransactionsQueryValidator : AbstractValidator<GetRecurringTransactionsQuery>
{
	public GetRecurringTransactionsQueryValidator()
	{
		RuleFor(x => x.PageSize)
			.InclusiveBetween(from: 1, to: 100).WithMessage(errorMessage: "The page size should be from 1 to 100.");

		RuleFor(x => x.CursorId).NotNull()
			.When(predicate: x => x.CursorCreatedAt is not null).WithMessage("CursorId must be provided together with CursorCreatedAt.");
		
		RuleFor(x => x.CursorCreatedAt).NotNull()
			.When(predicate: x => x.CursorId is not null).WithMessage("CursorCreatedAt must be provided together with CursorId.");
	}
}
