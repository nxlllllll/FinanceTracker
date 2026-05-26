using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;

public sealed record ActivateRecurringTransactionCommand(
	Guid UserId,
	Guid RecurringTransactionId
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;
