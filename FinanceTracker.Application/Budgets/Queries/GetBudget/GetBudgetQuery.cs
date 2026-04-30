using FinanceTracker.Core.Domains.Budget;
using MediatR;

namespace FinanceTracker.Application.Budgets.Queries.GetBudget;

public sealed record GetBudgetQuery(
	Guid UserId,
	Guid BudgetId
) : IRequest<Budget?>;