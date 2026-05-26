using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudgetProgress;

public sealed record GetBudgetProgressQuery(Guid BudgetId, Guid UserId) : IRequest<BudgetProgressDto?>, IUserScopedRequest;
