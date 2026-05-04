using FinanceTracker.Core.Domains.Budget;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Queries.GetBudgets;

public sealed record GetBudgetsQuery(Guid UserId) : IRequest<IReadOnlyList<Budget>>;