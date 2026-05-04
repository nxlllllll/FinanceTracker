using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetPeriod;

public sealed record ChangeBudgetPeriodCommand(
	Guid UserId,
	Guid BudgetId,
	DateOnly From,
	DateOnly To
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;