using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;

public sealed record DeactivateBudgetCommand(
	Guid UserId,
	Guid BudgetId
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;
