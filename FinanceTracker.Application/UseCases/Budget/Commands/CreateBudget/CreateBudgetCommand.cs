using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;

public sealed record CreateBudgetCommand(
	Guid UserId,
	Guid CategoryId,
	Core.ValueObjects.Currency Currency,
	decimal Amount,
	DateOnly From,
	DateOnly To
) : IIdempotentCommand, IRequest<Result<Guid, AppException>>, IUserScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}
