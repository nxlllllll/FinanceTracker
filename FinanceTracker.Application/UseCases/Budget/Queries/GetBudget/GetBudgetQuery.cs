using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;

public sealed record GetBudgetQuery(
	Guid BudgetId,
	Guid UserId
) : IRequest<Result<BudgetReadModel, DomainException>>, IUserScopedRequest;