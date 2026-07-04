using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed record ChangeRecurringTransactionDayOfMonthCommand(
	Guid UserId,
	Guid RecurringTransactionId,
	int DayOfMonth
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
