using FinanceTracker.Core.Domains.Transactions;
using MediatR;

namespace FinanceTracker.Application.Transactions.Queries.GetTransactions;

public sealed record GetTransactionsQuery(
	Guid AccountId,
	Guid? CategoryId = null,
	DirectionType? Direction = null,
	bool? IsExcluded = null,
	DateTime? DateFrom = null,
	DateTime? DateTo = null
) : IRequest<IReadOnlyList<Transaction>>;