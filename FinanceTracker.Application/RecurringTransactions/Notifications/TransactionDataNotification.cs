using FinanceTracker.Core.Domains.Account;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Notifications;

public record TransactionDataNotification(
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DirectionType Direction,
	string? Description,
	DateTime OccurredAt
) : INotification;