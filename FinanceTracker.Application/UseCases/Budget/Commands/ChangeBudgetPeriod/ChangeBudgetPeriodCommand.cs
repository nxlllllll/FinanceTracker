using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetPeriod;

public sealed record ChangeBudgetPeriodCommand(
	Guid UserId,
	Guid BudgetId,
	DateOnly From,
	DateOnly To
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
