using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;

public sealed record ActivateBudgetCommand(
	Guid UserId,
	Guid BudgetId
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
