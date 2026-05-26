using FinanceTracker.Application.Behaviours.RateLimit;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransaction;

public sealed record GetRecurringTransactionQuery(
	Guid UserId,
	Guid RecurringTransactionId
) : IRequest<Core.Domains.RecurringTransaction.RecurringTransaction>, IUserScopedRequest;
