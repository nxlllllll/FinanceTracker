using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotal;

public sealed record GetTotalQuery(
	Guid UserId,
	Guid CategoryId,
	DateOnly Period
) : IRequest<Result<CategoryTotal, AppException>>, IUserScopedRequest;
