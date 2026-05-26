using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;

public sealed record GetIncomeExpenseSummaryQuery(
	Guid UserId,
	DateOnly Period
) : IRequest<IncomeExpenseSummaryDto>, IUserScopedRequest;
