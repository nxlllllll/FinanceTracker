using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;
using INotification = MediatR.INotification;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Notifications;

public sealed record TransactionDataNotification(
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	Currency Currency,
	DirectionType Direction,
	string? Description,
	DateTime OccurredAt
) : INotification;