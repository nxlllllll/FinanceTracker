using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudgetProgress;

public sealed record GetBudgetProgressQuery(Guid BudgetId, Guid UserId) : IRequest<BudgetProgress?>, IUserScopedRequest;
