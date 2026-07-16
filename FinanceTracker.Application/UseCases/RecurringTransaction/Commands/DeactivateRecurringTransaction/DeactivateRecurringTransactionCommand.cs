using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.DeactivateRecurringTransaction;

public sealed record DeactivateRecurringTransactionCommand(
	Guid UserId,
	Guid RecurringTransactionId
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
