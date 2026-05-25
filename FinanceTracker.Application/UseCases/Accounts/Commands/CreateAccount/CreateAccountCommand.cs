using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Accounts.Commands.CreateAccount;

public sealed record CreateAccountCommand(
	Guid UserId,
	Name Name,
	AccountType Type,
	Currency Currency,
	decimal InitialBalance
) : IIdempotentCommand, IRequest<Result<Guid, DomainException>>, IUserScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}
