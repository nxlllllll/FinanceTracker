using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;

public sealed record ChangeRecurringTransactionCurrencyCommand(
	Guid UserId,
	Guid RecurringTransactionId,
	Currency Currency
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;