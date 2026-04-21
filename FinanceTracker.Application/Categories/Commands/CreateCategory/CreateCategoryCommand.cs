using FinanceTracker.Core.Domains.Category;
using MediatR;

namespace FinanceTracker.Application.Categories.Commands.CreateCategory;

public sealed record CreateCategoryCommand(
	Guid UserId,
	string Name,
	CategoryType Type,
	Guid? ParentId
) : IRequest;