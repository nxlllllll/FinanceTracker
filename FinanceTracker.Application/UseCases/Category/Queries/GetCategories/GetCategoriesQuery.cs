using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Category;
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
) : IRequest<Result<PagedResult<CategoryReadModel>, AppException>>, IUserScopedRequest;
