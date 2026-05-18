using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transfers.Commands;

public sealed record CreateTransferCommand(
	Guid UserId,
	Guid FromAccountId,
	Currency CurrencyFrom,
	Guid ToAccountId,
	Currency CurrencyTo,
	decimal Amount,
	string? Description,
	DateTime OccurredAt
) : IIdempotentCommand, IRequest<Result<Guid, DomainException>>, IAuthorizable
{
	public Guid IdempotencyKey { get; init; }
}