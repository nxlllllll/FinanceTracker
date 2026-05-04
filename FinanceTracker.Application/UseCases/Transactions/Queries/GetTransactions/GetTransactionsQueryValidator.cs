using FluentValidation;

namespace FinanceTracker.Application.UseCases.Transactions.Queries.GetTransactions;

public sealed class GetTransactionsQueryValidator : AbstractValidator<GetTransactionsQuery>
{
	public GetTransactionsQueryValidator()
	{
		RuleFor(x => x.PageSize)
			.InclusiveBetween(from: 1, to: 100).WithMessage(errorMessage: "The page size should be from 1 to 100.");
		
		RuleFor(x => x.CursorId).NotNull()
			.When(predicate: x => x.CursorOccurredAt is not null).WithMessage("CursorId must be provided together with CursorOccurredAt.");
		
		RuleFor(x => x.CursorOccurredAt).NotNull()
			.When(predicate: x => x.CursorId is not null).WithMessage("CursorOccurredAt must be provided together with CursorId.");
	}
}