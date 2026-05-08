using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetIncomeExpenseSummary;

public sealed record GetIncomeExpenseSummaryQuery(
	Guid UserId,
	DateOnly Period
) : IRequest<IncomeExpenseSummaryDto>;