using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;

public sealed record GetIncomeExpenseSummaryQuery(
	Guid UserId,
	DateOnly Period
) : IRequest<Result<IncomeExpenseSummary, AppException>>, IUserScopedRequest;
