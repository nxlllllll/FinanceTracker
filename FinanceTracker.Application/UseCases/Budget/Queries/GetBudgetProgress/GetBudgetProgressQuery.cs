using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudgetProgress;

public sealed record GetBudgetProgressQuery(
	Guid BudgetId,
	Guid UserId
) : IRequest<Result<BudgetProgress, AppException>>, IUserScopedRequest;
