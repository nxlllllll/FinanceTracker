using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Repositories.Category;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotalsByPeriod;

public sealed record GetTotalsByPeriodQuery(
	Guid UserId,
	DateOnly Period
) : IRequest<IReadOnlyList<CategoryTotal>>, IUserScopedRequest;
