using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Commands.CreateCategory;

public sealed record CreateCategoryCommand(
	Guid UserId,
	Name Name,
	CategoryType Type,
	Guid? ParentId
) : IIdempotentCommand, IRequest<Result<Guid, DomainException>>
{
	public Guid IdempotencyKey { get; init; }
}