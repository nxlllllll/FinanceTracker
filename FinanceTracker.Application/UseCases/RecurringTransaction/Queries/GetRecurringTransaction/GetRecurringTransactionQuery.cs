using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ReadModels;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransaction;

public sealed record GetRecurringTransactionQuery(
	Guid UserId,
	Guid RecurringTransactionId
) : IRequest<RecurringTransactionReadModel>, IUserScopedRequest;