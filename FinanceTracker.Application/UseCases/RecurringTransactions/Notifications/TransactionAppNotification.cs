using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Notifications;

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