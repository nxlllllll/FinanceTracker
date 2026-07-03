using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ReadModels;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotal;

public sealed record GetTotalQuery(
	Guid UserId,
	Guid CategoryId,
	DateOnly Period
) : IRequest<CategoryTotal?>, IUserScopedRequest;
