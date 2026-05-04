using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;

public sealed record ChangeRecurringTransactionAmountCommand(
	Guid UserId,
	Guid RecurringTransactionId,
	decimal Amount
) : IRequest<Guid>, IAuthorizable;