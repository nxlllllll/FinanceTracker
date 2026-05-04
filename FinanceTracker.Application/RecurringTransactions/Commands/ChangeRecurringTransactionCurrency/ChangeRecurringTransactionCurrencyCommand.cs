using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;

public sealed record ChangeRecurringTransactionCurrencyCommand(
	Guid UserId,
	Guid RecurringTransactionId,
	string Currency
) : IRequest<Guid>, IAuthorizable;