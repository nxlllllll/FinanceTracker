using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;

public sealed record ChangeRecurringTransactionCurrencyCommand(
	Guid UserId,
	Guid RecurringTransactionId,
	string Currency
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;