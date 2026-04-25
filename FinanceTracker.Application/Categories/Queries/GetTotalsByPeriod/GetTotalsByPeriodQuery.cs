using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.Categories.Queries.GetTotalsByPeriod;

public sealed record GetTotalsByPeriodQuery(
	Guid UserId,
	DateOnly Period
) : IRequest<IReadOnlyList<CategoryTotalDto>>;