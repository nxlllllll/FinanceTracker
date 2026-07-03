using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransaction;

public sealed record GetRecurringTransactionQuery(
	Guid RecurringTransactionId,
	Guid UserId
) : IRequest<Result<RecurringTransactionReadModel, DomainException>>, IUserScopedRequest;
