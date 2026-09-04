using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;

public sealed record CreateTransferCommand(
	Guid UserId,
	Guid FromAccountId,
	Guid ToAccountId,
	decimal Amount,
	string? Description
) : IIdempotentCommand, IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}
