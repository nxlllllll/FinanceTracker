using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;

public sealed record DeactivateRecurringTransactionCommand(
	Guid UserId,
	Guid RecurringTransactionId
) : IRequest, IAuthorizable;