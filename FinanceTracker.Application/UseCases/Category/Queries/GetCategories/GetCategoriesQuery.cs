using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetCategories;

public sealed record GetCategoriesQuery(
	Guid UserId,
	CategoryType? Type = null,
	bool? IsArchived = null,
	Guid? ParentId = null,
	DateTimeOffset? CursorCreatedAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<PagedResult<CategoryReadModel>>, IUserScopedRequest;