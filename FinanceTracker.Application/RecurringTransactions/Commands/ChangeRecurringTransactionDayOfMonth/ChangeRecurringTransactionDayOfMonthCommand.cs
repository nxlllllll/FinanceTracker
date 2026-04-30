using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed record ChangeRecurringTransactionDayOfMonthCommand(
	Guid UserId,
	Guid RecurringTransactionId,
	int DayOfMonth
) : IRequest, IAuthorizable;