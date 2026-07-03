using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ReadModels;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotalsByPeriod;

public sealed record GetTotalsByPeriodQuery(
	Guid UserId,
	DateOnly Period
) : IRequest<IReadOnlyList<CategoryTotal>>, IUserScopedRequest;
