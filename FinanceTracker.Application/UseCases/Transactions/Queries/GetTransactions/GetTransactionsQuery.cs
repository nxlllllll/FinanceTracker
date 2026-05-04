using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transactions.Queries.GetTransactions;

public sealed record GetTransactionsQuery(
	Guid AccountId,
	Guid? CategoryId = null,
	DirectionType? Direction = null,
	bool? IsExcluded = null,
	DateTime? DateFrom = null,
	DateTime? DateTo = null,
	DateTime? CursorOccurredAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<IReadOnlyList<Transaction>>;