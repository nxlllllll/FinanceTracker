using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.Budgets.Queries.GetBudgetProgress;

public sealed record GetBudgetProgressQuery(Guid BudgetId) : IRequest<BudgetProgressDto?>;