using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Commands.CreateBudget;

public sealed record CreateBudgetCommand(
	Guid UserId,
	Guid CategoryId,
	string Currency,
	decimal Amount,
	DateOnly From,
	DateOnly To
) : IRequest<Result<Guid, DomainException>>;