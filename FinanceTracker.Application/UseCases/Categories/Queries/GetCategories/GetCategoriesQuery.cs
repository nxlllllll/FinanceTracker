using FinanceTracker.Core.Domains.Category;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Queries.GetCategories;

public sealed record GetCategoriesQuery(
	Guid UserId,
	CategoryType? Type = null,
	bool? IsArchived = null,
	Guid? ParentId = null
) : IRequest<IReadOnlyList<Category>>;