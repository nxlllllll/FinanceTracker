using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;

public sealed record ChangeRecurringTransactionCurrencyCommand(
	Guid UserId,
	Guid RecurringTransactionId,
	Core.ValueObjects.Currency Currency
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;
