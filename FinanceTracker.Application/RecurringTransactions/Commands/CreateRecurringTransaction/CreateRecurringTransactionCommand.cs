using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.CreateRecurringTransaction;

public sealed record CreateRecurringTransactionCommand(
	Guid UserId,
	Guid AccountId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DirectionType Direction,
	int DayOfMonth,
	string? Description
) : IRequest<Guid>, IAuthorizable;