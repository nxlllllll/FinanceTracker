using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transfer.Commands;

public sealed record CreateTransferCommand(
	Guid UserId,
	Guid FromAccountId,
	Guid ToAccountId,
	decimal Amount,
	string? Description,
	DateTimeOffset OccurredAt
) : IIdempotentCommand, IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}
