using FinanceTracker.Core.Domains.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Queries.GetRecurringTransactions;

public sealed record GetRecurringTransactionsQuery(
	Guid UserId,
	DateTime? CursorCreatedAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<IReadOnlyList<RecurringTransaction>>;