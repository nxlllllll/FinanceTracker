using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;

public sealed record CreateCategoryCommand(
	Guid UserId,
	Name Name,
	CategoryType Type,
	Guid? ParentId
) : IIdempotentCommand, IRequest<Result<Guid, AppException>>, IUserScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}
