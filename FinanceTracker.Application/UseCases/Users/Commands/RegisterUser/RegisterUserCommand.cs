using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
	string Email,
	string Password,
	Currency BaseCurrencyCode
) : IIdempotentCommand, IRequest<Result<Guid, DomainException>>
{
	public Guid IdempotencyKey { get; init; }
}