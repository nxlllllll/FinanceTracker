using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;

public sealed record ChangeBudgetAmountCommand(
	Guid UserId,
	Guid BudgetId,
	decimal Amount
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
