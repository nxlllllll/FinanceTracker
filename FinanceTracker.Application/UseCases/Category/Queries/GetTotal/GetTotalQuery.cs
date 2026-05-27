using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Repositories.Category;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetTotal;

public sealed record GetTotalQuery(
	Guid UserId,
	Guid CategoryId,
	DateOnly Period
) : IRequest<CategoryTotal?>, IUserScopedRequest;
