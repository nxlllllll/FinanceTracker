using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Application.RecurringTransactions.Notifications;

public sealed record TransactionAppNotification(
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DirectionType Direction,
	string? Description,
	DateTime OccurredAt
) : IAppNotification
{
	public INotificationData Data { get; } = new TransactionData(
		AccountId: AccountId,
		UserId: UserId,
		CategoryId: CategoryId,
		Amount: Amount,
		Currency: Currency,
		Direction: Direction,
		Description: Description,
		OccurredAt: OccurredAt
	);
}