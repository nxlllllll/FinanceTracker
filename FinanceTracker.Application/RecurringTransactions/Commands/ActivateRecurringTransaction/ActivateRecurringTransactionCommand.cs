using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;

public sealed record ActivateRecurringTransactionCommand(
	Guid UserId,
	Guid RecurringTransactionId
) : IRequest<Guid>, IAuthorizable;