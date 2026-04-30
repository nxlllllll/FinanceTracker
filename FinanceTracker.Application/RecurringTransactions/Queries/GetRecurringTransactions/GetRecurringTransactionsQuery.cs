using FinanceTracker.Core.Domains.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Queries.GetRecurringTransactions;

public sealed record GetRecurringTransactionsQuery(Guid UserId) : IRequest<IReadOnlyList<RecurringTransaction>>;