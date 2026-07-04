using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;

public sealed record ChangeRecurringTransactionAmountCommand(
	Guid UserId,
	Guid RecurringTransactionId,
	decimal Amount
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
