using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.Budgets.Queries.GetBudget;

public sealed record GetBudgetQuery(
	Guid UserId,
	Guid BudgetId
) : IRequest<BudgetDto?>;