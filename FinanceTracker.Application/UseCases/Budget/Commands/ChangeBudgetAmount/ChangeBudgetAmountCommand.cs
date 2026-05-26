using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;

public sealed record ChangeBudgetAmountCommand(
	Guid UserId,
	Guid BudgetId,
	decimal Amount
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;
