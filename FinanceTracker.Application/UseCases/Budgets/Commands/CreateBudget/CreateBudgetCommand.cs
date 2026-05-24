using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Commands.CreateBudget;

public sealed record CreateBudgetCommand(
	Guid UserId,
	Guid CategoryId,
	Currency Currency,
	decimal Amount,
	DateOnly From,
	DateOnly To
) : IIdempotentCommand, IRequest<Result<Guid, DomainException>>, IUserScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}