using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.CreateRecurringTransaction;

public sealed record CreateRecurringTransactionCommand(
	Guid UserId,
	Guid AccountId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DirectionType Direction,
	int DayOfMonth,
	string? Description
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;