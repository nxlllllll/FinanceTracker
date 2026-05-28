using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ReadModels;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetCategory;

public sealed record GetCategoryQuery(
	Guid CategoryId,
	Guid UserId
) : IRequest<CategoryReadModel?>, IUserScopedRequest;