using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.CancelTransaction;

public sealed record CancelTransactionCommand(
	Guid UserId,
	Guid TransactionId
) : IIdempotentCommand, IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}
