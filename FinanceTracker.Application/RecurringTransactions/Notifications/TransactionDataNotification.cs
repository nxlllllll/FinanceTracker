using FinanceTracker.Core.Domains.Account;
using INotification = MediatR.INotification;

namespace FinanceTracker.Application.RecurringTransactions.Notifications;

public sealed record TransactionDataNotification(
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DirectionType Direction,
	string? Description,
	DateTime OccurredAt
) : INotification;