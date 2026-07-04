using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;

public sealed record ActivateRecurringTransactionCommand(
	Guid UserId,
	Guid RecurringTransactionId
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
