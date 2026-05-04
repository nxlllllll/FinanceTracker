using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Notifications;

public sealed class TransactionNotificationFactory : ITransactionNotificationFactory
{
	public IAppNotification Build(
		Guid accountId,
		Guid userId,
		Guid categoryId,
		decimal amount,
		string currency,
		DirectionType direction,
		string? description,
		DateTime occurredAt)
	{
		return new TransactionAppNotification(
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: amount,
			Currency: currency,
			Direction: direction,
			 Description: description,
			OccurredAt: occurredAt
		);
	}
}