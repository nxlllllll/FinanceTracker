using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;

public sealed record ChangeRecurringTransactionAmountCommand(
	Guid UserId,
	Guid RecurringTransactionId,
	decimal Amount
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;