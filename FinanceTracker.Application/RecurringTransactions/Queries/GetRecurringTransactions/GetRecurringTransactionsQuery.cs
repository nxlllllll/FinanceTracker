using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Queries.GetRecurringTransactions;

public sealed record GetRecurringTransactionsQuery(Guid UserId) : IRequest<IReadOnlyList<RecurringTransaction>>;