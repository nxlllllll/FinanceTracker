using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;

public sealed record CreateAccountCommand(
	Guid UserId,
	Name Name,
	AccountType Type,
	Core.ValueObjects.Currency Currency,
	decimal InitialBalance
) : IIdempotentCommand, IRequest<Result<Guid, AppException>>, IUserScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}
