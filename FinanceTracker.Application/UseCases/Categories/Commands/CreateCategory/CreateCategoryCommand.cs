using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Commands.CreateCategory;

public sealed record CreateCategoryCommand(
	Guid UserId,
	string Name,
	CategoryType Type,
	Guid? ParentId
) : IRequest<Result<Guid, DomainException>>;