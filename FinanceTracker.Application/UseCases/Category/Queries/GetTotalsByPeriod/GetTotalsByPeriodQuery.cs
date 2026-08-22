using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotalsByPeriod;

public sealed record GetTotalsByPeriodQuery(
	Guid UserId,
	DateOnly Period
) : IRequest<Result<CategoryTotalsView, AppException>>, IUserScopedRequest;
