using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ReadModels;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;

public sealed record GetBudgetQuery(
	Guid UserId,
	Guid BudgetId
) : IRequest<BudgetReadModel?>, IUserScopedRequest;