using FinanceTracker.Core.Domains.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Queries.GetRecurringTransaction;

public sealed record GetRecurringTransactionQuery(
	Guid UserId,
	Guid RecurringTransactionId
) : IRequest<RecurringTransaction>;