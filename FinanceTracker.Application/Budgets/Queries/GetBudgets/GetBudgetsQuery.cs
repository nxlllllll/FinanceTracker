using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.Budgets.Queries.GetBudgets;

public sealed record GetBudgetsQuery(Guid UserId) : IRequest<IReadOnlyList<Budget>>;