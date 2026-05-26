using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;

public sealed record CreateRecurringTransactionCommand(
	Guid UserId,
	Guid AccountId,
	Guid CategoryId,
	decimal Amount,
	Core.ValueObjects.Currency Currency,
	DirectionType Direction,
	int DayOfMonth,
	string? Description
) : IIdempotentCommand, IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}
