using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.RegisterUser;

public sealed record RegisterUserCommand(
	Email Email,
	string Password,
	Core.ValueObjects.Currency BaseCurrencyCode
) : IIdempotentCommand, IRequest<Result<Guid, DomainException>>
{
	public Guid IdempotencyKey { get; init; }
}
